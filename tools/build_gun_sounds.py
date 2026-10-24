#!/usr/bin/env python3
"""Synthesises the Gunslinger's sounds from noise and sine waves (no recordings, so no third-party licence), encodes
them with ffmpeg like tools/build_audio.py's effects (mono 44.1 kHz Ogg Vorbis, q4) and lists them in audio.json.

Usage: python3 tools/build_gun_sounds.py
  Needs numpy and scipy (not installed system-wide on the development machine; a venv works:
  python3 -m venv /tmp/snd && /tmp/snd/bin/pip install numpy scipy && /tmp/snd/bin/python tools/build_gun_sounds.py),
  and ffmpeg on the PATH. Fixed seeds: a re-run gives the same sounds.

Writes src/AshenReliquary.App/Assets/Audio/sfx/{gunshot-1..3, gunshot-heavy-1..2, reload-1, misfire-1}.ogg and the
effect ids "gunshot", "gunshot-heavy", "reload" and "misfire" in audio.json (each file's source "Ashen Reliquary,
synthesised by tools/build_gun_sounds.py", CC0). tools/build_audio.py keeps those entries when it rewrites the file.
"""
import json, os, subprocess, tempfile, wave
import numpy as np
from scipy import signal

RATE = 44100
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AUDIO = os.path.join(REPO, "src/AshenReliquary.App/Assets/Audio")
SOURCE = "Ashen Reliquary, synthesised by tools/build_gun_sounds.py"


def t_of(seconds):
    return np.arange(int(seconds * RATE)) / RATE


def band(x, lo=None, hi=None, order=2):
    if lo and hi:
        sos = signal.butter(order, [lo, hi], "bandpass", fs=RATE, output="sos")
    elif lo:
        sos = signal.butter(order, lo, "highpass", fs=RATE, output="sos")
    else:
        sos = signal.butter(order, hi, "lowpass", fs=RATE, output="sos")
    return signal.sosfilt(sos, x)


def place(out, x, at):
    i = int(at * RATE)
    n = min(len(x), len(out) - i)
    out[i:i + n] += x[:n]


def reverb(x, rng, decay=0.35, mix=0.25):
    """A small room: noise with an exponential tail as the impulse response."""
    t = t_of(decay * 2.5)
    ir = rng.standard_normal(len(t)) * np.exp(-t / (decay / 4))
    ir = band(ir, 200, 5000)
    wet = signal.fftconvolve(x, ir)[:len(x)]
    wet *= np.max(np.abs(x)) / (np.max(np.abs(wet)) + 1e-9)
    return x + mix * wet


def gunshot(seed, length, body_hz, boom_decay, tail_decay, crack_hp):
    rng = np.random.default_rng(seed)
    out = np.zeros(int(length * RATE))
    # The crack: a few milliseconds of bright noise with a sharp edge.
    t = t_of(0.012)
    crack = band(rng.standard_normal(len(t)), lo=crack_hp) * np.exp(-t / 0.0018)
    place(out, 1.0 * crack, 0.0)
    # The boom: low noise and a falling thump.
    t = t_of(length)
    boom = band(rng.standard_normal(len(t)), hi=900, order=4) * np.exp(-t / boom_decay)
    sweep = body_hz * (1 + 1.2 * np.exp(-t / 0.02))
    thump = np.sin(2 * np.pi * np.cumsum(sweep) / RATE) * np.exp(-t / (boom_decay * 0.8))
    attack = 1 - np.exp(-t / 0.0015)
    out += attack * (2.2 * boom / (np.max(np.abs(boom)) + 1e-9) * 0.5 + 0.55 * thump)
    # The tail: the shot rolling away.
    tail = band(rng.standard_normal(len(t)), 120, 1800) * np.exp(-t / tail_decay)
    out += 0.12 * tail / (np.max(np.abs(tail)) + 1e-9) * attack
    out = reverb(out, rng, decay=tail_decay, mix=0.3)
    fade = np.ones(len(out)); n = int(0.03 * RATE); fade[-n:] = np.linspace(1, 0, n)
    return out * fade


def metal_click(rng, freqs, decay, length=0.08):
    t = t_of(length)
    x = sum(np.sin(2 * np.pi * f * (1 + rng.uniform(-0.01, 0.01)) * t + rng.uniform(0, 6)) * np.exp(-t / (decay * rng.uniform(0.7, 1.2)))
            for f in freqs)
    noise = band(rng.standard_normal(len(t)), lo=3000) * np.exp(-t / 0.002)
    return x / len(freqs) + 0.6 * noise


def reload_sound(seed):
    rng = np.random.default_rng(seed)
    out = np.zeros(int(1.15 * RATE))
    # Powder poured from a flask: a stream of tiny grains, swelling and ebbing.
    t = t_of(0.4)
    grains = (rng.random(len(t)) < 0.02) * rng.standard_normal(len(t))
    pour = band(grains + 0.15 * rng.standard_normal(len(t)), 1500, 7000) * np.sin(np.pi * t / t[-1]) ** 0.7
    place(out, 0.5 * pour / np.max(np.abs(pour)), 0.02)
    # The ramrod: a scrape down the barrel and two taps on the charge.
    t = t_of(0.22)
    scrape = band(rng.standard_normal(len(t)), 700, 2600) * (0.3 + 0.7 * np.sin(np.pi * t / t[-1]))
    scrape += 0.4 * np.sin(2 * np.pi * 1180 * t) * band(rng.standard_normal(len(t)), hi=60)
    place(out, 0.35 * scrape / np.max(np.abs(scrape)), 0.48)
    for at in (0.72, 0.84):
        place(out, 0.8 * metal_click(rng, [1650, 2730, 4120], 0.025), at)
    # Cocking the hammer: a heavier double click.
    place(out, 0.9 * metal_click(rng, [2200, 3400, 5300], 0.012), 0.99)
    place(out, 1.0 * metal_click(rng, [1900, 3100, 4700], 0.018), 1.03)
    return reverb(out, rng, decay=0.12, mix=0.15)


def misfire_sound(seed):
    rng = np.random.default_rng(seed)
    out = np.zeros(int(0.32 * RATE))
    place(out, 1.0 * metal_click(rng, [2100, 3350, 5200, 7100], 0.014), 0.0)  # the hammer falls
    t = t_of(0.12)
    spark = band(rng.standard_normal(len(t)), lo=4500) * np.exp(-t / 0.03)  # a spark that catches nothing
    place(out, 0.18 * spark / np.max(np.abs(spark)), 0.006)
    place(out, 0.35 * metal_click(rng, [1500, 2600], 0.01), 0.07)  # the frizzen rattles
    return reverb(out, rng, decay=0.1, mix=0.12)


def normalise(x, peak_db):
    return x / np.max(np.abs(x)) * 10 ** (peak_db / 20)


def write(name, x):
    target = os.path.join(AUDIO, "sfx", name)
    with tempfile.TemporaryDirectory() as tmp:
        wav = os.path.join(tmp, "x.wav")
        with wave.open(wav, "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
            w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())
        subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", wav, "-ac", "1", "-ar", "44100", "-c:a", "libvorbis",
                        "-q:a", "4", target], check=True)
    rms = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)
    print(f"sfx/{name}: {len(x) / RATE:.2f} s, peak {20 * np.log10(np.max(np.abs(x))):.1f} dB, RMS {rms:.1f} dB")
    return f"sfx/{name}"


def main():
    effects = {
        "gunshot": [normalise(gunshot(11 + i, 0.55, hz, 0.05 + 0.01 * i, 0.16, hp), -6.0)
                    for i, (hz, hp) in enumerate([(110, 2500), (95, 3000), (125, 2200)])],
        "gunshot-heavy": [normalise(gunshot(31 + i, 0.95, hz, 0.11, 0.32, 1500), -5.0) for i, hz in enumerate([62, 55])],
        "reload": [normalise(reload_sound(41), -14.0)],
        "misfire": [normalise(misfire_sound(51), -13.0)],
    }
    with open(os.path.join(AUDIO, "audio.json")) as f:
        listing = json.load(f)
    for group, sounds in effects.items():
        files = [write(f"{group}-{i + 1}.ogg", x) for i, x in enumerate(sounds)]
        listing["effects"][group] = files
        listing["files"] = [e for e in listing["files"] if e["file"] not in files]
        listing["files"] += [{"file": name, "source": SOURCE, "url": "https://github.com/Harlock123/AVAAshenReliquary",
                              "original": "tools/build_gun_sounds.py", "licence": "CC0"} for name in files]
    with open(os.path.join(AUDIO, "audio.json"), "w") as f:
        json.dump(listing, f, indent=1)


if __name__ == "__main__":
    main()
