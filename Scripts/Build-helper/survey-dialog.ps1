# 설문 등록 창. Unity의 CurtainCall → 로그·설문 설정 창과 같은 항목을 편집한다.
# 확인을 누르면 편집한 복사본을 돌려주고, 파일 저장은 빌드를 실제로 시작할 때 launch.ps1이 한다.

function Show-SurveyDialog($Owner, $Settings, [string]$Version) {
    $edited = Copy-PlaytestSettings $Settings
    $dialog = [System.Windows.Forms.Form]@{
        Text = '설문 등록'; Font = $Owner.Font; StartPosition = 'CenterParent'; FormBorderStyle = 'FixedDialog'
        MaximizeBox = $false; MinimizeBox = $false; ClientSize = [System.Drawing.Size]::new(640, 700); AutoScaleMode = 'Dpi'
    }
    function Put($control, [int]$x, [int]$y, [int]$w, [int]$h, $parent) {
        $control.Location = [System.Drawing.Point]::new($x, $y); $control.Size = [System.Drawing.Size]::new($w, $h); $parent.Controls.Add($control); $control
    }
    function Text-Label([string]$text) { [System.Windows.Forms.Label]@{ Text = $text; TextAlign = 'MiddleLeft' } }

    $null = Put (Text-Label ("Google Forms에서 [미리 채워진 링크 받기] → '플레이 기록 연결 코드' 질문에 CC_LINK_SAMPLE 입력 → 링크 복사 후 아래 '미리 채워진 링크'에 붙여넣고 [가져오기]를 누르면 주소와 질문 ID가 채워집니다.`n" +
        '같은 설문 ID·회차는 PC 사용자마다 처음 한 번만 열립니다. 같은 설문을 다시 받으려면 회차를 올리고, 다른 설문이면 새 ID를 쓰세요.')) 12 8 616 64 $dialog

    $slots = @{}
    $y = 78
    foreach ($key in 'night', 'exit') {
        $survey = $edited.$key
        $group = Put ([System.Windows.Forms.GroupBox]@{ Text = $(if ($key -eq 'night') { '밤 정비 설문 (대기실 밤 정비 진입 시)' } else { '게임 종료 설문 (런 플레이 후 퇴장 시)' }) }) 12 $y 616 262 $dialog
        $c = @{ Survey = $survey }
        $c.Enabled = Put ([System.Windows.Forms.CheckBox]@{ Text = '사용'; Checked = [bool]$survey.enabled }) 12 20 100 24 $group
        $null = Put (Text-Label '설문 이름') 12 48 110 24 $group
        $c.Title = Put ([System.Windows.Forms.TextBox]@{ Text = [string]$survey.title }) 126 48 476 24 $group
        $null = Put (Text-Label '설문 ID') 12 78 110 24 $group
        $c.Id = Put ([System.Windows.Forms.TextBox]@{ Text = [string]$survey.id }) 126 78 200 24 $group
        $null = Put (Text-Label '회차') 340 78 60 24 $group
        $c.Round = Put ([System.Windows.Forms.NumericUpDown]@{ Minimum = 1; Maximum = 999999999; Value = [Math]::Max(1, [long]$survey.round) }) 404 78 100 24 $group
        $null = Put (Text-Label '대상 버전') 12 108 110 24 $group
        $c.Versions = Put ([System.Windows.Forms.TextBox]@{ Text = [string]$survey.versions }) 126 108 120 24 $group
        $null = Put (Text-Label '예: 0.2.2 / 0.2.x / *') 252 108 150 24 $group
        if ($key -eq 'night') {
            $null = Put (Text-Label '몇 일차 밤') 404 108 80 24 $group
            $c.Day = Put ([System.Windows.Forms.NumericUpDown]@{ Minimum = 1; Maximum = 99; Value = [Math]::Max(1, [long]$survey.nightDay) }) 488 108 60 24 $group
        } else {
            $null = Put (Text-Label '시점: 나가기·방장 종료·연결 끊김') 404 108 200 24 $group
        }
        $null = Put (Text-Label '설문 주소') 12 138 110 24 $group
        $c.Url = Put ([System.Windows.Forms.TextBox]@{ Text = [string]$survey.formUrl }) 126 138 476 24 $group
        $null = Put (Text-Label '자동 입력 질문 ID') 12 168 110 24 $group
        $c.Entry = Put ([System.Windows.Forms.TextBox]@{ Text = [string]$survey.entry }) 126 168 200 24 $group
        $null = Put (Text-Label '미리 채워진 링크') 12 200 110 24 $group
        $c.Prefill = Put ([System.Windows.Forms.TextBox]@{}) 126 200 380 24 $group
        $c.Import = Put ([System.Windows.Forms.Button]@{ Text = '가져오기'; Tag = $key }) 512 198 90 28 $group
        $c.Hint = Put (Text-Label '') 12 230 590 24 $group
        $c.Import.Add_Click({
            param($sender)
            $s = $slots[$sender.Tag]
            $uri = $null
            if (-not [Uri]::TryCreate($s.Prefill.Text.Trim(), 'Absolute', [ref]$uri)) { $s.Hint.ForeColor = [System.Drawing.Color]::Firebrick; $s.Hint.Text = '링크를 읽지 못했습니다.'; return }
            $match = [regex]::Match($uri.Query, '(?:[?&])(entry\.[0-9]+)=CC_LINK_SAMPLE(?:&|$)')
            if (-not $match.Success) { $s.Hint.ForeColor = [System.Drawing.Color]::Firebrick; $s.Hint.Text = '연결 코드 질문에 CC_LINK_SAMPLE을 넣은 미리 채워진 링크가 필요합니다.'; return }
            $s.Url.Text = $uri.GetLeftPart('Path'); $s.Entry.Text = $match.Groups[1].Value
            $s.Hint.ForeColor = [System.Drawing.Color]::DarkGreen; $s.Hint.Text = '주소와 질문 ID를 가져왔습니다.'
        })
        $slots[$key] = $c
        $y += 270
    }

    $summary = Put (Text-Label '') 12 ($y + 2) 616 24 $dialog
    $ok = Put ([System.Windows.Forms.Button]@{ Text = '확인' }) 436 ($y + 32) 92 32 $dialog
    $cancel = Put ([System.Windows.Forms.Button]@{ Text = '취소'; DialogResult = 'Cancel' }) 536 ($y + 32) 92 32 $dialog
    $dialog.CancelButton = $cancel

    $apply = {
        foreach ($key in 'night', 'exit') {
            $c = $slots[$key]; $s = $c.Survey
            $s.enabled = $c.Enabled.Checked; $s.title = $c.Title.Text.Trim(); $s.id = $c.Id.Text.Trim(); $s.round = [int]$c.Round.Value
            $s.versions = $c.Versions.Text.Trim(); $s.formUrl = $c.Url.Text.Trim(); $s.entry = $c.Entry.Text.Trim()
            if ($c.Day) { $s.nightDay = [int]$c.Day.Value }
        }
    }
    $refresh = {
        & $apply
        $names = @(Get-MatchingSurveys $edited $Version | ForEach-Object Label)
        $summary.ForeColor = if ($names.Count) { [System.Drawing.Color]::DarkGreen } else { [System.Drawing.Color]::Firebrick }
        $summary.Text = if ($names.Count) { "이번 빌드($Version)에 열릴 설문: $($names -join ', ')" } else { "이번 빌드($Version)에 열릴 설문이 없습니다. '사용'·대상 버전·주소를 확인하세요." }
    }
    foreach ($c in $slots.Values) {
        foreach ($field in @($c.Enabled, $c.Id, $c.Versions, $c.Url, $c.Entry, $c.Round, $c.Day)) {
            if (-not $field) { continue }
            if ($field -is [System.Windows.Forms.CheckBox]) { $field.Add_CheckedChanged($refresh) }
            elseif ($field -is [System.Windows.Forms.NumericUpDown]) { $field.Add_ValueChanged($refresh) }
            else { $field.Add_TextChanged($refresh) }
        }
    }
    $ok.Add_Click({
        & $apply
        $errors = foreach ($key in 'night', 'exit') {
            $s = $slots[$key].Survey
            if ($s.enabled) { $bad = Get-SurveyProblems $s; if ($bad.Count) { "$($slots[$key].Enabled.Parent.Text): $($bad -join ', ')" } }
        }
        if ($errors) { [void][System.Windows.Forms.MessageBox]::Show(($errors -join "`n"), '설문 등록 확인', 'OK', 'Warning'); return }
        $dialog.DialogResult = 'OK'
    })
    & $refresh
    $result = $dialog.ShowDialog($Owner)
    $dialog.Dispose()
    if ($result -eq 'OK') { $edited } else { $null }
}
