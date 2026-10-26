"""Procedural sound for Bus Driver.

The original had an AY chip and no sound at all in the Oric recreation; this
set adds a diesel engine, doors, the passenger bell, weather and UI feedback.
All waveforms are generated here so the repository carries no third-party
audio.
"""
from __future__ import annotations

import math
import os
import wave

import numpy as np

from common import AUDIO

RATE = 22050


def _write(name: str, samples: np.ndarray, peak: float = 0.85):
    os.makedirs(AUDIO, exist_ok=True)
    samples = np.asarray(samples, dtype=np.float64)
    m = np.max(np.abs(samples)) or 1.0
    samples = np.clip(samples / m * peak, -1, 1)
    pcm = (samples * 32767).astype(np.int16)
    with wave.open(os.path.join(AUDIO, name), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(pcm.tobytes())


def t(seconds: float) -> np.ndarray:
    return np.arange(int(RATE * seconds)) / RATE


def env(n: int, attack=0.004, decay=1.0, power=1.6) -> np.ndarray:
    a = max(1, int(attack * RATE))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a)
    tail = np.linspace(0, 1, n)
    e *= np.maximum(0, (1 - tail) ** power) * np.exp(-decay * tail)
    return e


def lowpass(x: np.ndarray, cutoff: float) -> np.ndarray:
    alpha = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    out = np.empty_like(x)
    prev = 0.0
    for i, s in enumerate(x):
        prev += alpha * (s - prev)
        out[i] = prev
    return out


def highpass(x: np.ndarray, cutoff: float) -> np.ndarray:
    return x - lowpass(x, cutoff)


def sine(freq, n, phase=0.0):
    return np.sin(2 * math.pi * freq * t(n / RATE) + phase)


def square(freq, n, duty=0.5):
    p = (freq * t(n / RATE)) % 1.0
    return np.where(p < duty, 1.0, -1.0)


def noise(n, seed=1):
    return np.random.default_rng(seed).uniform(-1, 1, n)


def periodic_noise(n, seed=1, harmonics=40):
    """A loop-safe noise bed built from harmonics of the loop length."""
    rng = np.random.default_rng(seed)
    seconds = n / RATE
    out = np.zeros(n)
    for k in range(1, harmonics + 1):
        amp = rng.normal() / k
        phase = rng.uniform(0, math.tau)
        out += amp * np.sin(2 * math.pi * k / seconds * t(seconds) + phase)
    return out


def engine_loop():
    dur = 2.0
    n = int(RATE * dur)
    time = t(dur)
    # A four-cylinder diesel firing note plus body resonance.
    base = 42.0
    sig = np.zeros(n)
    for mult, amp in ((1, 1.0), (2, 0.55), (3, 0.25), (4.5, 0.17), (6, 0.1)):
        sig += amp * np.sin(2 * math.pi * base * mult * time)
    sig += 0.12 * periodic_noise(n, 3, 24)
    # Slight uneven firing makes it sound like a diesel rather than a tone.
    sig *= 0.75 + 0.25 * np.sin(2 * math.pi * 3 * time) ** 2
    return sig


def rain_loop():
    n = int(RATE * 4.0)
    sig = periodic_noise(n, 11, 60) * 0.9
    sig = highpass(sig, 700)
    sig *= 0.58 + 0.42 * np.sin(2 * math.pi * 0.25 * t(4.0)) ** 2
    return sig


def door_open():
    n = int(RATE * 1.1)
    time = t(1.1)
    hiss = highpass(noise(n, 21), 1400) * env(n, 0.05, 1.2, 2.0)
    clunk = np.sin(2 * math.pi * 72 * time) * np.exp(-12 * time)
    spring = np.sin(2 * math.pi * 980 * time) * np.exp(-5 * time) * 0.25
    return hiss * 0.5 + clunk * 0.9 + spring


def door_close():
    n = int(RATE * 0.9)
    time = t(0.9)
    thud = np.sin(2 * math.pi * 58 * time) * np.exp(-18 * time)
    clack = highpass(noise(n, 33), 900) * np.exp(-20 * time) * 0.5
    latch = np.sin(2 * math.pi * 620 * time) * np.exp(-30 * time) * 0.2
    return thud + clack + latch


def bell():
    n = int(RATE * 1.2)
    time = t(1.2)
    sig = np.zeros(n)
    for f, a in ((1046, 1.0), (1568, 0.6), (2093, 0.25)):
        sig += a * np.sin(2 * math.pi * f * time) * np.exp(-2.4 * time)
    return sig


def chime():
    n = int(RATE * 1.8)
    time = t(1.8)
    sig = np.zeros(n)
    for start, freq in ((0.0, 784), (0.22, 988), (0.44, 1175)):
        k = int(start * RATE)
        seg = time[: n - k]
        sig[k:] += np.sin(2 * math.pi * freq * seg) * np.exp(-2.0 * seg) * 0.6
    return sig


def buzzer():
    n = int(RATE * 0.55)
    time = t(0.55)
    sig = (square(155, n) * 0.6 + square(116, n) * 0.4) * (0.5 + 0.5 * np.sin(2 * math.pi * 22 * time))
    return sig * env(n, 0.01, 1.2, 0.6)


def crash():
    n = int(RATE * 1.4)
    time = t(1.4)
    burst = lowpass(noise(n, 51), 1500) * np.exp(-4.5 * time) * 1.4
    thump = np.sin(2 * math.pi * 48 * time) * np.exp(-9 * time)
    glass = highpass(noise(n, 52), 4200) * np.exp(-12 * time) * 0.35
    return burst + thump + glass


def horn():
    n = int(RATE * 0.7)
    time = t(0.7)
    sig = np.sin(2 * math.pi * 392 * time) + 0.7 * np.sin(2 * math.pi * 494 * time)
    sig += 0.25 * np.sin(2 * math.pi * 784 * time)
    return sig * env(n, 0.01, 0.2, 0.5)


def indicator():
    n = int(RATE * 0.16)
    time = t(0.16)
    return (np.sin(2 * math.pi * 1320 * time) + 0.4 * square(2630, n)) * env(n, 0.002, 12, 1.0)


def ui_click():
    n = int(RATE * 0.09)
    time = t(0.09)
    return (np.sin(2 * math.pi * 740 * time) + 0.3 * sine(1480, n)) * env(n, 0.001, 22, 1.0)


def ui_confirm():
    n = int(RATE * 0.32)
    time = t(0.32)
    sig = np.zeros(n)
    for start, freq in ((0, 660), (0.11, 990)):
        k = int(start * RATE)
        seg = time[: n - k]
        sig[k:] += np.sin(2 * math.pi * freq * seg) * np.exp(-6 * seg)
    return sig


def fanfare():
    n = int(RATE * 2.6)
    time = t(2.6)
    sig = np.zeros(n)
    for start, freq, dur in ((0.0, 523, 0.35), (0.28, 659, 0.35),
                             (0.56, 784, 0.55), (1.08, 1047, 0.9)):
        k = int(start * RATE)
        seg = time[: n - k]
        gate = np.clip((dur - seg) / 0.05, 0, 1)
        sig[k:] += (np.sin(2 * math.pi * freq * seg) +
                    0.35 * np.sin(2 * math.pi * freq * 2 * seg)) * np.exp(-1.6 * seg) * gate * 0.5
    return sig


def skid():
    n = int(RATE * 0.8)
    time = t(0.8)
    body = highpass(noise(n, 71), 900) * 0.8
    body += np.sin(2 * math.pi * (210 + 60 * np.sin(2 * math.pi * 4 * time)) * time) * 0.15
    return body * env(n, 0.01, 0.4, 1.0)


def generate():
    files = {
        "engine_loop.wav": engine_loop(),
        "rain_loop.wav": rain_loop(),
        "door_open.wav": door_open(),
        "door_close.wav": door_close(),
        "bell.wav": bell(),
        "chime.wav": chime(),
        "buzzer.wav": buzzer(),
        "crash.wav": crash(),
        "horn.wav": horn(),
        "indicator.wav": indicator(),
        "ui_click.wav": ui_click(),
        "ui_confirm.wav": ui_confirm(),
        "fanfare.wav": fanfare(),
        "skid.wav": skid(),
    }
    for name, data in files.items():
        _write(name, data)
    return list(files)
