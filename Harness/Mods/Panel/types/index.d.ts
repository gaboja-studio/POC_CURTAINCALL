// todo 한 줄의 상태. 색은 register.tsx의 COLOR 표를 따른다.
// todo: 아직 안 함 / doing: 체크했지만 push 전 / saved: push됨 / submitted: PR에 포함 / cancelled: `- [-]`
export type ItemState = 'todo' | 'doing' | 'saved' | 'submitted' | 'cancelled'

export type TodoItem = { text: string; state: ItemState }

export type TodoSection = { title: string; items: TodoItem[] }

export type TaskView = {
  id: string
  title: string
  assignee: string
  branch: string
  integration: string
  sections: TodoSection[]
  isMine: boolean
  canSave: boolean
  canSubmit: boolean
  hasNext: boolean
}

export type PrView = {
  number: number
  title: string
  branch: string
  taskId: string | null
  mergeable: 'MERGEABLE' | 'CONFLICTING' | 'UNKNOWN'
  isDraft: boolean
}

export type TaskLink = { id: string; title: string; branch: string }

export type Snapshot = {
  branch: string
  worktree: string
  isLinkedWorktree: boolean
  login: string | null
  pm: string | null
  mode: 'task' | 'integration' | 'idle'
  tasks: TaskView[]
  prs: PrView[]
  myOtherTasks: TaskLink[]
  done: TaskLink[]
  error: string | null
}

declare module 'claude-code' {
  interface PluginState {
    'curtaincall-panel': { snapshot: Snapshot | null; tab: number; isHidden: boolean }
  }
}
