'use strict';
const $ = id => document.getElementById(id);
const token = location.hash.slice(1) || sessionStorage.getItem('asset-helper-session') || '';
if (token) sessionStorage.setItem('asset-helper-session', token);
history.replaceState(null, '', '/');
let state = null, busy = false;
const labels = { ready: '에셋 버전 일치', mismatch: '에셋 버전 확인 필요', edited: '내 에셋 변경 있음', missing: '에셋 받기 필요', incomplete: '일부 에셋 받기 필요' };
function notice(text, error = false) { $('notice').textContent = text; $('notice').classList.toggle('error', error); }
function setBusy(value) { busy = value; for (const id of ['refresh', 'publish', 'sync', 'login', 'confirm-publish', 'confirm-sync', 'keep-sync', 'cancel', 'cancel-sync']) $(id).disabled = value || (!state && ['publish', 'sync'].includes(id)) || (id === 'publish' && !state?.initialized); }
async function api(path, body) {
  const response = await fetch(path, { method: body ? 'POST' : 'GET', headers: { 'X-Asset-Helper': token, ...(body ? { 'Content-Type': 'application/json' } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
  const data = await response.json();
  if (!response.ok) throw new Error(data.error || '요청을 처리하지 못했습니다.');
  return data;
}
async function run(action, extra = {}) {
  if (busy) return;
  setBusy(true);
  try {
    const started = await api('/api/action', { action, snapshot: state?.snapshot, ...extra });
    let result;
    do {
      await new Promise(resolve => setTimeout(resolve, 650));
      result = await api('/api/job/' + started.job);
      if (!result.done) notice(result.message);
    } while (!result.done);
    if (!result.response.ok) throw new Error(result.response.error);
    return result.response.result;
  } catch (error) { notice(error.message || '도우미에 연결하지 못했습니다. 아이콘으로 다시 열어 주세요.', true); return null; }
  finally { setBusy(false); }
}
function showState(next) {
  state = next;
  $('branch').textContent = state.branch || '브랜치 선택 필요';
  $('badge').textContent = labels[state.status];
  $('badge').classList.toggle('warn', state.status !== 'ready');
  $('publish').disabled = !state.initialized;
  $('sync').disabled = false;
  notice(state.status === 'ready' ? '현재 브랜치의 에셋 버전과 같습니다. 에셋 변경 후에는 저장·공유를 눌러 주세요.' : state.status === 'edited' ? `에셋 변경 ${state.changes.length}개가 있습니다. 공유하거나, 보관한 뒤 브랜치에 맞출 수 있습니다.` : '현재 브랜치에 에셋 맞추기를 눌러 준비해 주세요.');
  renderShare(state.share);
  $('backups').hidden = !state.backups?.length;
  $('backup-list').replaceChildren();
  for (const backup of state.backups || []) {
    const row = document.createElement('p'), button = document.createElement('button');
    button.className = 'secondary';
    button.textContent = `${new Date(backup.date).toLocaleString('ko-KR')} · ${backup.projectBranch} 작업 복구`;
    button.onclick = async () => { if (busy || !confirm('Unity를 닫았나요? 보관했던 에셋 작업을 복구합니다.')) return; const result = await run('restore', { backup: backup.id }); if (result) { await refresh(); showResult(result); } };
    row.append(button); $('backup-list').append(row);
  }
}
async function refresh() { const next = await run('status'); if (next) { showState(next); return true; } return false; }
function renderShare(share) {
  $('share-status').hidden = !share;
  if (!share) return;
  const pendingTitles = { unknown: '팀 공유 여부 확인 실패 · 완료로 처리하지 않음', local_changes: '팀 공유 미완료 · 내 수정 저장 필요', version_mismatch: '현재 에셋 버전 확인 필요 · 공유 완료로 처리하지 않음' };
  $('share-title').textContent = share.teamReady ? '팀 공유 완료 · origin/dev 반영 확인' : pendingTitles[share.stage] || '팀 공유 미완료 · 통합 대기';
  $('share-steps').replaceChildren();
  for (const [text, done] of [['비공개 에셋 원격 업로드', share.privateUploaded], ['게임 작업 브랜치 원격 업로드', share.taskUploaded], ['팀 공용 origin/dev 반영 확인', share.teamReady]]) {
    const li = document.createElement('li'); li.textContent = `${done ? '✓ 완료' : '○ 대기·확인 필요'} — ${text}`; $('share-steps').append(li);
  }
  if (share.cleanup) {
    const li = document.createElement('li');
    li.textContent = `${share.cleanup.done ? '✓ 임시 항목 정리 완료' : '○ 임시 항목 정리 보류'} — ${share.cleanup.messages.join(' ')}`;
    $('share-steps').append(li);
  }
  const link = share.teamPr || share.url;
  $('share-link').hidden = !link;
  if (link) $('share-link').href = link;
  const pendingMessages = { private_pending: '팀 공유 미완료입니다. 비공개 에셋 통합 후 같은 버튼으로 dev 검토 요청까지 이어가세요.', local_changes: '내 에셋 수정이 아직 저장·공유되지 않았습니다. 변경 확인하고 공유를 눌러 주세요.', version_mismatch: '현재 브랜치와 내 에셋 버전이 다릅니다. 버전을 확인하고 진행해 주세요.', unknown: '원격 공유 여부를 확인하지 못했습니다. 완료로 처리하지 않습니다. 연결 후 다시 확인해 주세요.' };
  if (!share.teamReady) notice(pendingMessages[share.stage] || '팀 공유 미완료입니다. 검토·병합 후 다시 확인해 origin/dev 반영을 확인하세요.', true);
}
function showResult(result) {
  if (!result) return;
  $('details').hidden = $('sync-details').hidden = true;
  $('result').hidden = false;
  const titles = { private_pending: '팀 공유 미완료 · 에셋 통합 대기', dev_pending: '팀 공유 미완료 · dev 반영 대기', team_shared: '팀 공유 완료 · origin/dev 반영 확인', synced: '내 PC의 에셋 버전을 맞췄습니다', restored: '내 PC에 보관한 수정을 복구했습니다', unchanged: '새로 저장한 변경 없음', login: 'GitHub 연결 확인' };
  $('result-title').textContent = result.title || titles[result.kind] || '처리 결과를 확인해 주세요';
  $('result-message').textContent = result.message;
  $('review-link').hidden = !result.url;
  if (result.url) $('review-link').href = result.url;
  $('kept').textContent = result.kept ? `보관 기록: ${result.kept}${result.stash ? ' · 내 수정은 보관 목록에서 복구할 수 있습니다.' : ''}` : '';
  notice(result.message);
  if (result.share) renderShare(result.share);
}
$('refresh').onclick = refresh;
$('publish').onclick = async () => {
  if (!await refresh() || !state || busy) return;
  $('result').hidden = $('sync-details').hidden = true; $('details').hidden = false;
  $('change-title').textContent = `변경된 에셋 ${state.changes.length}개`;
  $('other').textContent = `다른 게임 변경 ${state.otherCount}개는 제외`;
  $('files').replaceChildren();
  for (const change of state.changes) { const li = document.createElement('li'), label = document.createElement('span'); label.textContent = change.label; li.append(label, document.createTextNode(change.path)); $('files').append(li); }
  if (!state.changes.length) { const li = document.createElement('li'); li.textContent = '이전에 저장한 에셋 또는 연결 표시의 공유를 이어갑니다.'; $('files').append(li); }
  $('verified').checked = false;
  $('details').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
};
$('sync').onclick = async () => {
  if (!await refresh() || !state || busy) return;
  $('details').hidden = $('result').hidden = true; $('sync-details').hidden = false;
  $('closed').checked = false;
  $('keep-sync').hidden = !state.changes.length;
  $('confirm-sync').hidden = !!state.changes.length;
  $('sync-details').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
};
$('confirm-publish').onclick = async () => {
  if (!$('verified').checked) { notice('Unity 저장과 오류·기능 확인을 마쳤는지 체크해 주세요.', true); return; }
  if ($('message').value.trim().length < 2) { notice('무엇을 바꿨는지 한 줄로 적어 주세요.', true); $('message').focus(); return; }
  const result = await run('publish', { message: $('message').value.trim() }); if (result) { await refresh(); showResult(result); }
};
async function sync(keep) { if (!$('closed').checked) { notice('Unity를 닫았는지 체크해 주세요.', true); return; } const result = await run('sync', { keep }); if (result) { await refresh(); showResult(result); } }
$('confirm-sync').onclick = () => sync(false);
$('keep-sync').onclick = () => sync(true);
$('cancel').onclick = () => { $('details').hidden = true; };
$('cancel-sync').onclick = () => { $('sync-details').hidden = true; };
$('login').onclick = async () => showResult(await run('login'));
if (!token) notice('프로젝트 폴더의 에셋 도우미 아이콘으로 열어 주세요.', true); else refresh();
