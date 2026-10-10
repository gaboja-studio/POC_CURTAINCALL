import { expect, test } from 'claude-code/testing'

import { classify, hasNextItem, parseBranchStatus, parseMeta, parsePm, sameLogin } from '../hooks/logic'

const TODO = (marks: { a: string; b: string; c: string; d: string }) => `# Todo

## PM 준비

- [x] 범위 작성

## 작업자

- [${marks.a}] 첫 작업
- [${marks.b}] 둘째 작업
- [${marks.c}] 셋째 작업
- [${marks.d}] 넷째 작업
`

test('todo 상태: 체크 위치(작업 중·push·PR)에 따라 색이 정해진다', () => {
  const submitted = TODO({ a: 'x', b: ' ', c: ' ', d: ' ' })
  const pushed = TODO({ a: 'x', b: 'x', c: ' ', d: ' ' })
  const working = TODO({ a: 'x', b: 'x', c: 'x', d: '-' })
  const sections = classify(working, pushed, submitted)
  const worker = sections.find(section => section.title === '작업자')
  expect(worker?.items.map(item => item.state)).toEqual(['submitted', 'saved', 'doing', 'cancelled'])
})

test('todo 상태: push 전·PR 전이면 체크한 항목은 모두 진행함(노랑)', () => {
  const working = TODO({ a: 'x', b: ' ', c: ' ', d: ' ' })
  const sections = classify(working, null, null)
  expect(sections[1]?.items.map(item => item.state)).toEqual(['doing', 'todo', 'todo', 'todo'])
  expect(hasNextItem(sections)).toBe(true)
})

test('다음 할 일은 작업자 절만 본다', () => {
  const working = TODO({ a: 'x', b: 'x', c: 'x', d: '-' }).replace('- [x] 범위 작성', '- [ ] 범위 작성')
  expect(hasNextItem(classify(working, null, null))).toBe(false)
})

test('meta.md와 team.md를 읽는다', () => {
  const meta = parseMeta(`- **Title:** 줄타기 코어
- **Assignee:** @MoHoDu
- **Integration:** integration/tightrope
- **Branch:** feat/tightrope-core
- **Status:** assigned`)
  expect(meta).toEqual({
    title: '줄타기 코어',
    assignee: 'MoHoDu',
    branch: 'feat/tightrope-core',
    integration: 'integration/tightrope',
    status: 'assigned',
  })
  expect(parsePm('| 개발 PM | `@MoHoDu` | PM 전용 영역 수정 권한, 병합 담당 |')).toBe('MoHoDu')
  expect(sameLogin('mohodu', 'MoHoDu')).toBe(true)
  expect(sameLogin(null, 'MoHoDu')).toBe(false)
})

test('git status 출력에서 upstream·ahead·변경 여부를 읽는다', () => {
  const clean = parseBranchStatus('# branch.oid abc\n# branch.head feat/x\n# branch.upstream origin/feat/x\n# branch.ab +2 -0\n')
  expect(clean).toEqual({ upstream: 'origin/feat/x', ahead: 2, isDirty: false })
  const dirty = parseBranchStatus('# branch.head feat/x\n1 .M N... 100644 100644 100644 a b Tasks/x.md\n')
  expect(dirty).toEqual({ upstream: null, ahead: 0, isDirty: true })
})
