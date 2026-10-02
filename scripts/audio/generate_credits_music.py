"""Render Salinlahi's original, quiet 64-second ambient credits loop.

Requires NumPy and FFmpeg on the author's machine, not in the game.
No sampled recordings, percussion, noise, or third-party melodies.
"""
from pathlib import Path
import subprocess
import tempfile
import wave

import numpy as np


def render():
    rate = 44100
    length = 64 * rate
    mix = np.zeros(length, dtype=np.float64)

    def add_note(midi, start, duration, gain, mallet=False):
        t = np.arange(round(duration * rate)) / rate
        frequency = 440 * 2 ** ((midi - 69) / 12)
        tone = np.sin(2 * np.pi * frequency * t)
        tone += (0.14 if mallet else 0.06) * np.sin(4 * np.pi * frequency * t)
        if mallet:
            envelope = (1 - np.exp(-t / 0.07)) * np.exp(-t / 1.5)
        else:
            envelope = np.minimum(t / 2.5, 1) * np.minimum((duration - t) / 4, 1)
            envelope = np.sin(np.clip(envelope, 0, 1) * np.pi / 2) ** 2
        # Fold tails across the seam so the musical loop has no sudden cutoff.
        indices = (round(start * rate) + np.arange(len(t))) % length
        np.add.at(mix, indices, gain * tone * envelope)

    chords = [(48, 55, 59, 64), (43, 50, 57, 59), (45, 52, 55, 60), (41, 48, 52, 57)]
    for bar in range(8):
        for note in chords[bar % 4]:
            add_note(note, bar * 8, 12, 0.11)
    melody = [(2, 67), (6, 64), (11, 62), (18, 64), (23, 60), (27, 65),
              (34, 64), (39, 67), (44, 62), (50, 64), (55, 60), (60, 65)]
    for start, note in melody:
        add_note(note, start, 6, 0.12, mallet=True)
    mix -= mix.mean()
    # Conservative source level before the additional in-game credits multiplier.
    gain = min(0.22 / np.max(np.abs(mix)), 0.065 / np.sqrt(np.mean(mix ** 2)))
    pcm = np.round(mix * gain * 32767).astype('<i2')
    destination = Path(__file__).resolve().parents[2] / 'Assets/Resources/Audio/Credits/credits-ambient.ogg'
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='salinlahi-credits-audio-') as temporary:
        source = Path(temporary) / 'credits.wav'
        with wave.open(str(source), 'wb') as output:
            output.setnchannels(1)
            output.setsampwidth(2)
            output.setframerate(rate)
            output.writeframes(pcm.tobytes())
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(source),
                        '-c:a', 'libvorbis', '-q:a', '5', str(destination)], check=True)
    print(destination)
    print(f'64 s; peak {20*np.log10(np.max(np.abs(mix*gain))):.1f} dBFS; '
          f'RMS {20*np.log10(np.sqrt(np.mean((mix*gain)**2))):.1f} dBFS')


if __name__ == '__main__':
    render()
