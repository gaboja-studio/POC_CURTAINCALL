import { atom, read, update } from 'claude-code'
import type { EngineInterface, Register } from 'claude-code'

import type { ItemState, PrView, Snapshot, TaskLink, TaskView } from '../types'
import {
  classify,
  hasNextItem,
  parseBranchStatus,
  parseMeta,
  parsePm,
  sameLogin,
} from './logic'

const snapshot = atom({ plugin: 'curtaincall-panel', key: 'snapshot' } as const, null)
const tab = atom({ plugin: 'curtaincall-panel', key: 'tab' } as const, 0)
const isHidden = atom({ plugin: 'curtaincall-panel', key: 'isHidden' } as const, false)

const FAST_MS = 8_000 // git·파일 다시 읽기
const GH_MS = 60_000 // gh(네트워크) 다시 묻기
const DONE_LIMIT = 6

const COLOR: Record<ItemState, string> = {
  todo: 'text',
  doing: 'warning',
  saved: 'success',
  submitted: 'ide',
  cancelled: 'error',
}
const MARK: Record<ItemState, string> = {
  todo: '[ ]',
  doing: '[x]',
  saved: '[x]',
  submitted: '[x]',
  cancelled: '[-]',
}
const MERGEABLE: Record<PrView['mergeable'], { text: string; color: string }> = {
  MERGEABLE: { text: '병합 가능', color: 'success' },
  CONFLICTING: { text: '충돌', color: 'error' },
  UNKNOWN: { text: '확인 중', color: 'inactive' },
}

const EMPTY: Snapshot = {
  branch: '?',
  worktree: '?',
  isLinkedWorktree: false,
  login: null,
  pm: null,
  mode: 'idle',
  tasks: [],
  prs: [],
  myOtherTasks: [],
  done: [],
  error: null,
}

type BranchPr ={ number: number; state: string; headRefOid: string }

// gh 결과는 자주 바뀌지 않으므로 모듈 변수에 잠시 둔다(다시 로드되면 새로 묻는다).
let login: string | null = null
let ghAt = 0
const branchPrs = new Map<string, BranchPr | null>()
const basePrs = new Map<string, PrView[]>()
let isRefreshing = false

const run = async ($: EngineInterface, argv: string[], cwd?: string) => {
  try {
    const ran = await $.process.run(argv, { cwd, timeoutMs: 15_000 })
    return ran.exitCode === 0 ? ran.stdout : null
  } catch {
    return null
  }
}

const readText = async ($: EngineInterface, path: string) => {
  try {
    return await $.fs.read(path)
  } catch {
    return null
  }
}

const listDirs = async ($: EngineInterface, path: string) => {
  try {
    const entries = await $.fs.list(path)
    return entries.filter(entry => entry.kind === 'dir').map(entry => entry.name).sort()
  } catch {
    return []
  }
}

const count = async ($: EngineInterface, range: string) => {
  const out = await run($, ['git', 'rev-list', '--count', range])
  return out === null ? 0 : Number(out.trim()) || 0
}

const refreshGh = async ($: EngineInterface, branch: string, isIntegration: boolean) => {
  if (login === null) {
    const out = await run($, ['gh', 'api', 'user', '--jq', '.login'])
    login = out === null ? null : out.trim() || null
  }
  if (isIntegration) {
    const out = await run($, [
      'gh', 'pr', 'list', '--base', branch, '--state', 'open',
      '--json', 'number,title,headRefName,mergeable,isDraft',
    ])
    const rows: Array<Record<string, unknown>> = out === null ? [] : JSON.parse(out)
    basePrs.set(
      branch,
      rows.map(row => ({
        number: Number(row.number),
        title: String(row.title),
        branch: String(row.headRefName),
        taskId: null,
        mergeable: (['MERGEABLE', 'CONFLICTING'].includes(String(row.mergeable))
          ? row.mergeable
          : 'UNKNOWN') as PrView['mergeable'],
        isDraft: row.isDraft === true,
      })),
    )
  } else {
    const out = await run($, [
      'gh', 'pr', 'list', '--head', branch, '--state', 'all',
      '--json', 'number,state,headRefOid', '--limit', '5',
    ])
    const rows: BranchPr[] = out === null ? [] : JSON.parse(out)
    const pr = rows.find(row => row.state === 'OPEN') ?? rows.find(row => row.state === 'MERGED')
    branchPrs.set(branch, pr ?? null)
  }
  ghAt = await $.clock.now()
}

// skip: gh 없이 git·파일만 / auto: gh 결과가 오래됐으면 다시 묻기 / force: gh도 지금 묻기
type GhMode = 'skip' | 'auto' | 'force'
const GH_RANK: Record<GhMode, number> = { skip: 0, auto: 1, force: 2 }
let pending: GhMode | null = null

// 갱신 중에 들어온 요청은 버리지 않고, 끝난 뒤 가장 강한 요청으로 한 번 더 돈다.
const refresh = async ($: EngineInterface, gh: GhMode) => {
  if (isRefreshing) {
    if (pending === null || GH_RANK[gh] > GH_RANK[pending]) pending = gh
    return
  }
  isRefreshing = true
  try {
    const next = await build($, gh)
    await update($, snapshot, () => next)
  } catch (error) {
    // 처음부터 실패해도 패널은 떠서 이유를 보여 준다.
    const message = error instanceof Error ? error.message : String(error)
    await update($, snapshot, old => ({ ...(old ?? EMPTY), error: message }))
  } finally {
    isRefreshing = false
  }
  if (pending !== null) {
    const again = pending
    pending = null
    await refresh($, again)
  }
}

const build = async ($: EngineInterface, gh: GhMode): Promise<Snapshot> => {
  const top = (await run($, ['git', 'rev-parse', '--show-toplevel']))?.trim()
  if (!top) throw new Error('git 저장소가 아님')
  const branch = (await run($, ['git', 'rev-parse', '--abbrev-ref', 'HEAD']))?.trim() ?? '?'
  const gitDir = (await run($, ['git', 'rev-parse', '--git-dir']))?.trim() ?? ''
  const commonDir = (await run($, ['git', 'rev-parse', '--git-common-dir']))?.trim() ?? ''
  const isIntegration = branch.startsWith('integration/')

  const isStale = login === null || (await $.clock.now()) - ghAt > GH_MS
  if (gh === 'force' || (gh === 'auto' && isStale)) {
    await refreshGh($, branch, isIntegration).catch(() => undefined)
  }

  const pm = parsePm((await readText($, `${top}/Harness/Project/Facts/team.md`)) ?? '')

  // 진행 중 Task 메타 모두 읽기
  const activeRoot = `${top}/Tasks/Active`
  const active: Array<{ dir: string; id: string; meta: ReturnType<typeof parseMeta> }> = []
  for (const dir of await listDirs($, activeRoot)) {
    const metaText = await readText($, `${activeRoot}/${dir}/meta.md`)
    if (metaText === null) continue
    active.push({ dir, id: dir.match(/^Task-\d+-\d+/)?.[0] ?? dir, meta: parseMeta(metaText) })
  }

  const base: Snapshot = {
    branch,
    worktree: top.split('/').pop() ?? top,
    isLinkedWorktree: gitDir !== '' && commonDir !== '' && gitDir !== commonDir && gitDir !== '.git',
    login,
    pm,
    mode: 'idle',
    tasks: [],
    prs: [],
    myOtherTasks: [],
    done: [],
    error: null,
  }

  if (isIntegration) {
    const prs = (basePrs.get(branch) ?? []).map(pr => ({
      ...pr,
      taskId: active.find(task => task.meta.branch === pr.branch)?.id ?? null,
    }))
    return { ...base, mode: 'integration', prs }
  }

  const mine = active.filter(task => task.meta.branch === branch)
  if (mine.length === 0) {
    const doneRoot = `${top}/Tasks/Done`
    const doneDirs = (await listDirs($, doneRoot)).reverse().slice(0, DONE_LIMIT)
    const done: TaskLink[] = []
    for (const dir of doneDirs) {
      const meta = parseMeta((await readText($, `${doneRoot}/${dir}/meta.md`)) ?? '')
      done.push({ id: dir.match(/^Task-\d+-\d+/)?.[0] ?? dir, title: meta.title, branch: meta.branch })
    }
    const myOtherTasks = active
      .filter(task => sameLogin(task.meta.assignee, login))
      .map(task => ({ id: task.id, title: task.meta.title, branch: task.meta.branch }))
    return { ...base, mode: 'idle', done, myOtherTasks }
  }

  // 현재 브랜치의 Task: git 상태로 저장·제출 여부 판단
  const status = parseBranchStatus(
    (await run($, ['git', 'status', '--porcelain=v2', '--branch'])) ?? '',
  )
  const pr = branchPrs.get(branch) ?? null
  const tasks: TaskView[] = []
  for (const task of mine) {
    const path = `Tasks/Active/${task.dir}/todo.md`
    const working = (await readText($, `${top}/${path}`)) ?? ''
    const pushed = status.upstream ? await run($, ['git', 'show', `${status.upstream}:${path}`]) : null
    const submitted = pr ? await run($, ['git', 'show', `${pr.headRefOid}:${path}`]) : null
    const sections = classify(working, pushed, submitted)
    const target = `origin/${task.meta.integration || 'dev'}`
    const unpushed = status.upstream === null ? await count($, `${target}..HEAD`) : status.ahead
    const pushedAhead = status.upstream ? await count($, `${target}..${status.upstream}`) : 0
    tasks.push({
      id: task.id,
      title: task.meta.title,
      assignee: task.meta.assignee,
      branch: task.meta.branch,
      integration: task.meta.integration,
      sections,
      isMine: sameLogin(task.meta.assignee, login),
      canSave: status.isDirty || unpushed > 0,
      canSubmit: pr === null && pushedAhead > 0,
      hasNext: hasNextItem(sections),
    })
  }
  return { ...base, mode: 'task', tasks }
}

export const register: Register = on => {
  on('session.start', async ($, e, next) => {
    await $.command.register({
      name: 'curtaincall-panel',
      description: 'CURTAINCALL 진행 패널 보이기/숨기기',
    })
    // git·파일만 먼저 읽어 패널을 바로 띄우고, 느린 gh는 뒤에서 묻는다.
    await refresh($, 'skip')
    void refresh($, 'force')
    $.clock.every(FAST_MS, () => void refresh($, 'auto'))
    return next(e)
  })

  on('command.run', { command: 'curtaincall-panel' }, async $ => {
    let hidden = false
    await update($, isHidden, value => {
      hidden = !value
      return hidden
    })
    if (!hidden) void refresh($, 'force')
    return { text: hidden ? 'CURTAINCALL 패널을 숨겼습니다.' : 'CURTAINCALL 패널을 표시합니다.' }
  })

  on('turn.complete', async ($, e, next) => {
    const result = await next(e)
    void refresh($, 'force')
    return result
  })

  on('ui.render', { component: 'AbovePrompt' }, async ($, e, next) => {
    const data = await read($, snapshot)
    if (e.props.hasSurvey || data === null) return next(e)

    const { Box, Button, Text } = $.ui.resolve(e)
    // 숨긴 상태: 한 줄만 남겨 다시 펼칠 수 있게 한다.
    if (await read($, isHidden)) {
      return (
        <Box columnGap={1}>
          <Text dimColor>CURTAINCALL 패널 숨김 · {data.branch}</Text>
          <Button key="show" label="보이기" plain onPress={() => update($, isHidden, () => false)} />
        </Box>
      )
    }
    const isWorking = e.props.isWorking
    const selected = Math.min(await read($, tab), Math.max(0, data.tasks.length - 1))

    const ask = (text: string) => () => {
      void $.prompt.submit({ text, asUser: true })
    }
    // 비활성 버튼: 흐리게 그리고 눌러도 아무 일도 하지 않는다.
    const action = (key: string, label: string, isOn: boolean, text: string) => (
      <Button key={key} label={label} dimColor={!isOn} variant={isOn ? 'primary' : 'secondary'}
        onPress={isOn ? ask(text) : () => undefined} />
    )
    const hide = (
      <Button key="hide" label="숨기기" plain dimColor onPress={() => update($, isHidden, () => true)} />
    )

    const who = data.login ? `@${data.login}${sameLogin(data.login, data.pm) ? ' (PM)' : ''}` : 'gh 미연결'
    const header = (
      <Box columnGap={2}>
        <Text bold>{data.branch}</Text>
        <Text dimColor>{data.isLinkedWorktree ? `워크트리 ${data.worktree}` : `저장소 ${data.worktree}`}</Text>
        <Text dimColor>{who}</Text>
        {data.error && <Text color="error">갱신 실패: {data.error}</Text>}
      </Box>
    )

    if (data.mode === 'integration') {
      const canMerge = sameLogin(data.login, data.pm) && data.prs.length > 0 && !isWorking
      const slug = data.branch.replace(/^integration\//, '')
      return (
        <Box flexDirection="column">
          {header}
          <Text dimColor>통합 브랜치로 올라온 PR {data.prs.length}개</Text>
          {data.prs.map(pr => (
            <Box key={`pr-${pr.number}`} columnGap={1}>
              <Text color={pr.isDraft ? 'inactive' : MERGEABLE[pr.mergeable].color}>
                {pr.isDraft ? '초안' : MERGEABLE[pr.mergeable].text}
              </Text>
              <Text wrap="truncate-end">
                #{pr.number} {pr.taskId ?? '(Task 없음)'} {pr.title}
              </Text>
            </Box>
          ))}
          <Box columnGap={1}>
            {action('merge', '합치자', canMerge, `${slug} 통합 병합 진행해줘`)}
            {hide}
          </Box>
        </Box>
      )
    }

    if (data.mode === 'idle') {
      return (
        <Box flexDirection="column">
          {header}
          <Text color="warning">진행 중인 TASK 없음</Text>
          {data.myOtherTasks.length > 0 && <Text dimColor>내 진행 중 Task (다른 브랜치)</Text>}
          {data.myOtherTasks.map(task => (
            <Text key={`mine-${task.id}`} wrap="truncate-end">  {task.id} {task.title} · {task.branch}</Text>
          ))}
          <Text dimColor>최근 완료 Task</Text>
          {data.done.map(task => (
            <Text key={`done-${task.id}`} color="ide" wrap="truncate-end">  [x] {task.id} {task.title}</Text>
          ))}
          <Box>{hide}</Box>
        </Box>
      )
    }

    const task = data.tasks[selected]
    if (!task) return next(e)
    const isAllowed = task.isMine && task.branch === data.branch && !isWorking
    return (
      <Box flexDirection="column">
        {header}
        {data.tasks.length > 1 && (
          <Box columnGap={1}>
            {data.tasks.map((one, index) => (
              <Button key={`tab-${one.id}`} label={one.id} plain dimColor={index !== selected}
                onPress={() => update($, tab, () => index)} />
            ))}
          </Box>
        )}
        <Text bold wrap="truncate-end">
          {task.id} {task.title} <Text dimColor>· 담당 @{task.assignee}</Text>
        </Text>
        {task.sections.map(section => (
          <Box key={`sec-${section.title}`} flexDirection="column">
            <Text dimColor>{section.title}</Text>
            {section.items.map((item, index) => (
              <Text key={`item-${section.title}-${index}`} color={COLOR[item.state]}
                strikethrough={item.state === 'cancelled'} wrap="truncate-end">
                {'  '}{MARK[item.state]} {item.text}
              </Text>
            ))}
          </Box>
        ))}
        <Box columnGap={1}>
          {action('save', '저장해줘', isAllowed && task.canSave, `${task.id} 저장해줘`)}
          {action('submit', '제출해줘', isAllowed && task.canSubmit, `${task.id} 제출해줘`)}
          {action('next', '진행해줘', isAllowed && task.hasNext, `${task.id} 이어서 해줘`)}
          {hide}
        </Box>
      </Box>
    )
  })
}
