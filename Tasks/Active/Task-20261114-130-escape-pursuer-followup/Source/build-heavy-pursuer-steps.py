"""Derive weighty foot/landing sounds from the project's own existing recordings.
No model, external sample, random download, or original asset is modified.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from scipy import signal
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[4]
SOURCE = ROOT / 'Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer'
OUT = ROOT / '.harness/local/pursuer-weight-20261116/audio'
OUT.mkdir(parents=True, exist_ok=True)
RATE = 48000

def read(name):
    rate, pcm = wavfile.read(SOURCE / name)
    data = pcm.astype(np.float64) / np.iinfo(pcm.dtype).max
    if data.ndim > 1:
        data = data.mean(axis=1)
    return signal.resample_poly(data, RATE, rate)

def band(x, lo, hi):
    return signal.sosfilt(signal.butter(3, [lo, hi], btype='bandpass', fs=RATE, output='sos'), x)

def length(x, count):
    return np.pad(x[:count], (0, max(0, count-len(x))))

step = read('UnderstageHeavyStep.wav')
concrete = read('UnderstageConcreteAndSteps.wav')[int(2.3*RATE):int(3.1*RATE)]
report = dict(source_sha256={p.name: hashlib.sha256(p.read_bytes()).hexdigest()
    for p in [SOURCE/'UnderstageHeavyStep.wav', SOURCE/'UnderstageConcreteAndSteps.wav']}, clips={})
clips = []
for i, name in enumerate(['HeavyFootfallL', 'HeavyFootfallR', 'HeavyBodyImpact']):
    duration = .86 if i < 2 else 1.08
    count = round(duration*RATE)
    time = np.arange(count)/RATE
    body = length(signal.resample(step, round(len(step)*(1.10 if i == 0 else 1.16))), count)
    grit = length(signal.resample(concrete, round(len(concrete)*(1.02 + i*.035))), count)
    # Preserve the organic 53 Hz body, add audible low-mid concrete weight, and
    # remove the sharp crack. No pitched oscillator is added to the recording.
    low = band(body, 32, 145)
    mid = band(grit, 85, 280)
    texture = band(grit, 280, 1050)
    x = low + .32*mid + .065*texture
    if i == 2:
        x += .26 * length(np.pad(band(grit, 42, 200), (int(.065*RATE), 0)), count)
    # A quiet room/body reflection leaves weight after contact without a bright ring.
    delay = round((.082 + i*.009)*RATE)
    x += .14*length(np.pad(x.copy(), (delay, 0)), count)
    x *= np.minimum(1, time/.003) * np.minimum(1, (duration-time)/.14)
    x -= x.mean()
    x *= np.minimum(1, time/.002) * np.minimum(1, (duration-time)/.02)
    x *= 10**(-3.5/20) / max(np.max(np.abs(x)), 1e-12)
    wavfile.write(OUT/(name+'.wav'), RATE, np.round(x*32767).astype(np.int16))
    freq, power = signal.welch(x, RATE, nperseg=4096)
    report['clips'][name] = dict(seconds=duration, peak_db=float(20*np.log10(np.abs(x).max())),
        rms_db=float(20*np.log10(np.sqrt(np.mean(x*x)))),
        energy_above_1500=float(power[freq>1500].sum()/power.sum()),
        energy_60_300=float(power[(freq>=60)&(freq<=300)].sum()/power.sum()))
    assert np.isfinite(x).all() and np.abs(x).max() < .71 and abs(x[-1]) < .001
    assert report['clips'][name]['energy_above_1500'] < .01
    clips.append(x)

preview = np.zeros(RATE*6)
for i in range(6):
    start = round((.3 + i*.715)*RATE)
    x = clips[i%2]
    preview[start:start+len(x)] += x*.8
assert np.abs(preview).max() < 1
wavfile.write(OUT/'HeavyRunPreview.wav', RATE, np.round(preview*32767).astype(np.int16))
(OUT/'audio-verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
