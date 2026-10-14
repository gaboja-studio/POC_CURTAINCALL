import { expect, test } from 'claude-code/testing'
import type { Engine } from 'claude-code/testing'
import type { On } from 'claude-code'

// git·gh·파일을 가짜로 답하는 작은 저장소. 테스트의 on 훅은 모드 아래에서 엔진 대신 답한다.
type World = {
  branch: string
  login: string
  status: string
  files: Record<string, string>
  shows: Record<string, string>
  basePrs?: unknown[]
}

const TEAM = '| 개발 PM | `@MoHoDu` | PM 전용 영역 수정 권한, 병합 담당 |'
const META = (assignee: string, branch: string) => `- **Title:** 패널 테스트
- **Assignee:** @${assignee}
- **Integration:** integration/panel
- **Branch:** ${branch}`
const TODO = (a: string, b: string, c: string) => `## 작업자

- [${a}] 끝난 일
- [${b}] 취소한 일
- [${c}] 남은 일
`
const TASK_DIR = 'Task-20261010-001-panel'

const fake = (on: On, world: World) => {
  const ok = (stdout: string) => ({ value: { exitCode: 0, stdout, stderr: '', isStdoutTruncated: false, isStderrTruncated: false } })
  const fail = { value: { exitCode: 128, stdout: '', stderr: 'no', isStdoutTruncated: false, isStderrTruncated: false } }
  on('process.run', async ($, e) => {
    const cmd = e.argv.join(' ')
    if (cmd === 'git rev-parse --show-toplevel') return ok('/repo\n')
    if (cmd === 'git rev-parse --abbrev-ref HEAD') return ok(`${world.branch}\n`)
    if (cmd === 'git rev-parse --git-dir') return ok('/repo/.git/worktrees/panel\n')
    if (cmd === 'git rev-parse --git-common-dir') return ok('/repo/.git\n')
    if (cmd.startsWith('git status')) return ok(world.status)
    if (cmd.startsWith('git rev-list --count')) return ok('1\n')
    if (cmd.startsWith('git show ')) {
      const shown = world.shows[e.argv[2] ?? '']
      return shown === undefined ? fail : ok(shown)
    }
    if (cmd.startsWith('gh api user')) return ok(`${world.login}\n`)
    if (cmd.startsWith('gh pr list --base')) return ok(JSON.stringify(world.basePrs ?? []))
    if (cmd.startsWith('gh pr list --head')) return ok('[]')
    return fail
  })
  on('session.start', async ($, e) => ({ cwd: e.cwd }))
  on('command.register', async ($, e) => ({ value: { command: e.name } }))
  on('clock.every', async () => ({ value: undefined }))
  on('clock.now', async () => ({ value: 1_000_000 }))
  // 엔진은 경로를 OS 형식으로 바꿔 넘긴다(Windows: D:\repo\...). 비교용으로 /repo/... 로 되돌린다.
  const posix = (path: string) => path.replace(/\\/g, '/').replace(/^[A-Za-z]:/, '')
  on('fs.read', async ($, e) => {
    const text = world.files[posix(e.path)]
    return text === undefined ? { deny: `없음: ${e.path}` } : { value: text }
  })
  on('fs.list', async ($, e) => {
    const prefix = `${posix(e.path)}/`
    const names = new Set(
      Object.keys(world.files)
        .filter(path => path.startsWith(prefix))
        .map(path => path.slice(prefix.length).split('/')[0] ?? ''),
    )
    return { value: [...names].map(name => ({ name, kind: 'dir' as const, size: 0, mtimeMs: 0, isLink: false })) }
  })
}

const BAND = {
  plugin: 'curtaincall-panel',
  surface: 'terminal',
  component: 'AbovePrompt',
  props: {
    hasSurvey: false,
    isWorking: false,
    maxRows: 30,
    bodyColumns: 100,
    scroll: { offset: 0, bodyRows: 30 },
    view: {},
  },
} as const

const taskWorld = (login: string): World => ({
  branch: 'feat/panel',
  login,
  status: '# branch.head feat/panel\n# branch.upstream origin/feat/panel\n# branch.ab +0 -0\n1 .M N... 1 1 1 a b x\n',
  files: {
    '/repo/Harness/Project/Facts/team.md': TEAM,
    [`/repo/Tasks/Active/${TASK_DIR}/meta.md`]: META('MoHoDu', 'feat/panel'),
    [`/repo/Tasks/Active/${TASK_DIR}/todo.md`]: TODO('x', '-', ' '),
  },
  shows: { [`origin/feat/panel:Tasks/Active/${TASK_DIR}/todo.md`]: TODO('x', ' ', ' ') },
})

// 모드의 첫 갱신(session.start)과 뒤이은 gh 갱신이 끝날 때까지 기다린다.
const start = async ($: Engine, until: RegExp) => {
  await $.session.start({ cwd: '/repo', surface: 'terminal', isInteractive: true })
  const ui = await $.ui.mount(BAND)
  for (let tries = 0; tries < 200; tries += 1) {
    if (await ui.find({ type: 'Text', text: until })) break
  }
  return ui
}

test('Task 모드: todo 색과 버튼 활성 상태', async ($, on) => {
  fake(on, taskWorld('MoHoDu'))
  const sent: string[] = []
  on('prompt.submit', async ($, e) => {
    sent.push(e.text)
    return { text: e.text }
  })
  const ui = await start($, /@MoHoDu/)
  expect((await ui.find({ type: 'Text', text: /끝난 일/ }))?.props.color).toBe('success')
  expect((await ui.find({ type: 'Text', text: /취소한 일/ }))?.props.strikethrough).toBe(true)
  expect((await ui.find({ type: 'Text', text: /남은 일/ }))?.props.color).toBe('text')
  expect((await ui.find({ key: 'save' }))?.props.dimColor).toBe(false)
  expect((await ui.find({ key: 'submit' }))?.props.dimColor).toBe(false)
  expect((await ui.find({ key: 'next' }))?.props.dimColor).toBe(false)
  await ui.press({ key: 'next' })
  await ui.press({ key: 'save' })
  expect(sent).toEqual(['Task-20261010-001 이어서 해줘', 'Task-20261010-001 저장해줘'])
  await ui.unmount()
})

test('숨기기를 누르면 한 줄로 줄고, 보이기로 다시 펼친다', async ($, on) => {
  fake(on, taskWorld('MoHoDu'))
  const ui = await start($, /@MoHoDu/)
  await ui.press({ key: 'hide' })
  expect(await ui.find({ type: 'Text', text: /패널 숨김/ })).toBeDefined()
  expect(await ui.find({ type: 'Text', text: /남은 일/ })).toBeUndefined()
  await ui.press({ key: 'show' })
  expect(await ui.find({ type: 'Text', text: /남은 일/ })).toBeDefined()
  await ui.unmount()
})

test('담당자가 아니면 버튼이 모두 꺼진다', async ($, on) => {
  fake(on, taskWorld('someone'))
  const ui = await start($, /@someone/)
  for (const key of ['save', 'submit', 'next']) {
    expect((await ui.find({ key }))?.props.dimColor).toBe(true)
  }
  await ui.unmount()
})

const integrationWorld = (login: string): World => ({
  branch: 'integration/panel',
  login,
  status: '',
  files: {
    '/repo/Harness/Project/Facts/team.md': TEAM,
    [`/repo/Tasks/Active/${TASK_DIR}/meta.md`]: META('worker', 'feat/panel'),
  },
  shows: {},
  basePrs: [{ number: 7, title: '패널', headRefName: 'feat/panel', mergeable: 'MERGEABLE', isDraft: false }],
})

test('통합 브랜치: PR과 연결 Task를 보여 주고 PM만 합치자가 켜진다', async ($, on) => {
  fake(on, integrationWorld('MoHoDu'))
  const ui = await start($, /#7 Task-20261010-001/)
  expect(await ui.find({ type: 'Text', text: /병합 가능/ })).toBeDefined()
  expect((await ui.find({ key: 'merge' }))?.props.dimColor).toBe(false)
  await ui.unmount()
})

test('통합 브랜치: PM이 아니면 합치자가 꺼진다', async ($, on) => {
  fake(on, integrationWorld('worker'))
  const ui = await start($, /@worker/)
  expect((await ui.find({ key: 'merge' }))?.props.dimColor).toBe(true)
  await ui.unmount()
})

test('Task가 없으면 "진행 중인 TASK 없음"과 완료 목록', async ($, on) => {
  fake(on, {
    branch: 'dev',
    login: 'MoHoDu',
    status: '',
    files: {
      '/repo/Harness/Project/Facts/team.md': TEAM,
      '/repo/Tasks/Done/Task-20260930-001-overview/meta.md': '- **Title:** 개요',
    },
    shows: {},
  })
  const ui = await start($, /Task-20260930-001 개요/)
  expect(await ui.find({ type: 'Text', text: /진행 중인 TASK 없음/ })).toBeDefined()
  await ui.unmount()
})
