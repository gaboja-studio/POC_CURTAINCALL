import type { ItemState, TodoSection } from '../types'

// 파일 내용만 받아 계산하는 순수 함수 모음. git·gh 호출은 register.tsx가 한다.

export type Meta = {
  title: string
  assignee: string
  branch: string
  integration: string
  status: string
}

const field = (text: string, name: string): string => {
  const found = text.match(new RegExp(`\\*\\*${name}:\\*\\*\\s*(.+)`))
  return found?.[1] ? found[1].trim().replace(/^`|`$/g, '') : ''
}

export const parseMeta = (text: string): Meta => ({
  title: field(text, 'Title'),
  assignee: field(text, 'Assignee').replace(/^@/, ''),
  branch: field(text, 'Branch'),
  integration: field(text, 'Integration'),
  status: field(text, 'Status'),
})

// team.md 표의 "개발 PM | `@id`" 행에서 PM 계정을 찾는다.
export const parsePm = (text: string): string | null => {
  const found = text.match(/PM\s*\|\s*`?@([\w-]+)`?/)
  return found?.[1] ?? null
}

type RawItem = { text: string; mark: ' ' | 'x' | '-' }
type RawSection = { title: string; items: RawItem[] }

const ITEM = /^\s*[-*]\s+\[([ xX-])\]\s+(.*)$/

export const parseTodo = (text: string): RawSection[] => {
  const sections: RawSection[] = []
  let current: RawSection | null = null
  for (const line of text.split(/\r?\n/)) {
    const heading = line.match(/^##\s+(.+)$/)
    if (heading) {
      current = { title: (heading[1] ?? '').trim(), items: [] }
      sections.push(current)
      continue
    }
    const item = line.match(ITEM)
    if (!item) continue
    if (!current) {
      current = { title: '', items: [] }
      sections.push(current)
    }
    const mark = (item[1] ?? ' ').toLowerCase() as RawItem['mark']
    current.items.push({ text: (item[2] ?? '').trim(), mark })
  }
  return sections.filter(section => section.items.length > 0)
}

// 해당 버전의 todo.md에서 [x]로 체크된 항목 글자 집합. 버전이 없으면 빈 집합.
export const checkedIn = (text: string | null): Set<string> => {
  const checked = new Set<string>()
  if (text === null) return checked
  for (const section of parseTodo(text)) {
    for (const item of section.items) {
      if (item.mark === 'x') checked.add(item.text)
    }
  }
  return checked
}

// 작업 중 파일을 기준으로, push된 버전(pushed)과 PR 버전(submitted)에 같은 항목이
// 이미 체크돼 있는지 보고 색 상태를 정한다.
export const classify = (
  working: string,
  pushed: string | null,
  submitted: string | null,
): TodoSection[] => {
  const inPushed = checkedIn(pushed)
  const inSubmitted = checkedIn(submitted)
  return parseTodo(working).map(section => ({
    title: section.title,
    items: section.items.map(item => {
      let state: ItemState = 'todo'
      if (item.mark === '-') state = 'cancelled'
      else if (item.mark === 'x') {
        state = inSubmitted.has(item.text)
          ? 'submitted'
          : inPushed.has(item.text)
            ? 'saved'
            : 'doing'
      }
      return { text: item.text, state }
    }),
  }))
}

// 작업자가 다음에 할 항목이 있는지. "작업자" 절이 있으면 그 절만, 없으면 전체를 본다.
export const hasNextItem = (sections: TodoSection[]): boolean => {
  const worker = sections.filter(section => section.title.includes('작업자'))
  const scope = worker.length > 0 ? worker : sections
  return scope.some(section => section.items.some(item => item.state === 'todo'))
}

export type BranchStatus = {
  upstream: string | null
  ahead: number
  isDirty: boolean
}

// `git status --porcelain=v2 --branch` 출력 해석.
export const parseBranchStatus = (text: string): BranchStatus => {
  let upstream: string | null = null
  let ahead = 0
  let isDirty = false
  for (const line of text.split(/\r?\n/)) {
    if (line.startsWith('# branch.upstream ')) upstream = line.slice(18).trim()
    else if (line.startsWith('# branch.ab ')) {
      const found = line.match(/\+(\d+)/)
      ahead = found ? Number(found[1]) : 0
    } else if (line.length > 0 && !line.startsWith('#')) isDirty = true
  }
  return { upstream, ahead, isDirty }
}

export const sameLogin = (a: string | null, b: string | null): boolean =>
  a !== null && b !== null && a.length > 0 && a.toLowerCase() === b.toLowerCase()
