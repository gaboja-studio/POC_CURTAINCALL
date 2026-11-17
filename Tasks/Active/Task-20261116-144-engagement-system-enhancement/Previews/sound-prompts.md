# 관객 반응 사운드 생성 프롬프트

이 문서는 오디오 생성 모델에 전달할 제작 지시다. 채팅의 재생 예시는 아래 프롬프트로 생성한 녹음이 아니라, 방향·리듬·길이를 비교하기 위한 코드 합성 시안이다. 실제 사람의 군중 소리와 자연스러운 공간감은 후속 생성/녹음에서 검토한다.

## 공통 제작 지시

Create original, production-ready game sound effects for a dark, oppressive industrial circus, seen from a third-person performer balancing on ropes high above a deep arena. Anonymous ADULT spectators sit 30–60 meters away in elevated circular tiers. They enjoy dangerous acts with cruel indifference. The game is cooperative physical comedy: sinister human amusement, not supernatural horror. Make success and score loss instantly distinguishable by contour and texture, even with the screen unseen. This is a brief EVENT cue, never a music bed or continuous ambience.

Keep the identity of both cues consistent: distant human crowd + a very short, worn brass/mechanical tonal accent. A small dry leading accent makes the scoring event legible; the crowd then establishes distance. Localized onset spreads into a broad stereo dome, with a few soft early reflections and a dark 1.0–1.4-second tail. Preserve a stable center; no extreme panning. Crowd intensity must not resemble a close microphone. Avoid piercing 2–5 kHz energy, heavy sub-bass, clipping, brickwall loudness, musical orchestration, speech, announcers, intelligible words, children, screams of pain, monster voices, evil villain laughter, splatter, gunshots, explosions, cartoon boings, cash-register rewards, casino jingles, modern stadium chants, whistles, alarms, or long musical endings. Do not mimic any existing game recording or identifiable person.

Deliver a 3.6-second stereo WAV at 48 kHz / 24-bit if supported; leave headroom and a complete natural tail. For the two cue masters, match perceived loudness, target roughly −20 to −18 LUFS integrated for initial audition and peak no higher than −3 dBTP; these are mix starting points, verify after generation. Also deliver separate leading-accent, crowd, and room-tail stems if supported. Produce three modest natural variations per cue, preserving recognition, length, and envelope. Do not claim unsupported export/stem features; prioritize the audible result.

## 호응 상승 — 전체 프롬프트 뒤에 추가

Generate the CHEER / SCORE INCREASE event. It must read as approval immediately, but approval from a demanding, indifferent crowd, not a wholesome victory celebration.
0.00–0.24 s: a restrained two-note upward figure, approximately a minor third, on a muted, slightly unstable old brass tone. Short, low in the mix, tactile and dry, not a bright trumpet fanfare. No more than two notes.
0.12–0.50 s: a compact group of adults begins an unmistakable enthusiastic open-vowel cheer and irregular handclaps, as if pleased by a risky stunt. Human and nonverbal; a few breathy amused chuckles may be buried deep, never a solo laugh.
0.50–1.60 s: approval spreads to an adjoining seating bank, with uneven handclaps and overlapping cheers. Avoid a synchronized sports chant. The energy rises then releases; slight unease comes from how pleased they are, not from distorted monster timbre.
1.60–2.30 s: crowd quickly settles, applause thins.
2.30–3.60 s: only a dark, soft dome decay, ending naturally. Keep the rope and friends' voice chat perceptually available. The cue should feel like “they enjoyed that; they expect more.” No spoken line.

## 호응 하락 — 전체 프롬프트 뒤에 추가

Generate the BOO / SCORE DECREASE event. It must read as social disapproval and score loss immediately, not character injury, death, a monster approaching, or a failed-button error.
0.00–0.24 s: the same muted old brass/mechanical timbre as the positive cue, but a brief downward minor-third figure. An understated dull metal contact may reinforce the start. Do not make a buzzer or comedic trombone slide.
0.20–0.45 s: a small pocket of tense quiet in THIS effect only; never mute other game sounds. The initial descending accent has already communicated the score loss.
0.35–1.70 s: a distant group of adults produces unmistakable low human “boo / oo” disapproval, not intelligible speech. Add two or three restrained wooden footfalls or palms striking the spectator rail, slightly imperfect but sharing an impatient pulse. No earthquake bass or violent impact.
1.70–2.30 s: murmuring recedes into the seating tier, leaving the sense of being watched and judged.
2.30–3.60 s: soft dark room tail. The downward contour and rounded crowd vowels must clearly differ from the upward, brighter applause cue. Use the same perceived loudness as CHEER; disapproval should be oppressive through rhythm and unanimity, not louder volume.

## 반복 발생용 짧은 버전

After approving the full pair, derive a 0.7–1.0-second micro version of each: retain the same initial upward/downward accent, one very short crowd gesture, and a shortened natural tail. Do not replay full crowd swells for every rapid point event. Full arena reactions need independent rate limiting and coalescing; ordinary points still update the existing gauge accurately. Sound does not create new score events or penalties.

## 청취 판단

- 화면을 보지 않고도 상승/하락 구분이 되는가. 처음 약 0.3초 안에 방향이 읽히는가.
- 관객이 멀고 높은 곳에 있으며, 사람의 불쾌한 즐거움/불만으로 들리는가.
- 친구 음성과 외줄 위험 소리를 가리지 않는가. 비슷한 체감 음량인가.
- 10회 반복해도 놀람·피로가 과하지 않은가. 실제 믹스와 2~4인 중복 사건은 별도 검증한다.
