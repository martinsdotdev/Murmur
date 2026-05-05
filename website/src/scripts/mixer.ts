/**
 * Tiny web-audio mixer for the Soundscape Demo.
 *
 * Design mirrors Murmur/Services/AudioGraphMixerService.cs:
 *  - Volume > 0  ⇒ active (no epsilon, no hysteresis — strict zero is "off")
 *  - Volume === 0 ⇒ stop & detach the source (re-create on next rise)
 *  - Master Play/Pause is implemented by ramping the master GainNode, NOT by
 *    suspending the AudioContext. Suspended contexts can't accept new sources
 *    cleanly across browsers.
 *
 * Browser autoplay policies require the AudioContext to be created from a
 * user gesture, so `init()` must be called from a click/tap handler — typically
 * the master Play button or the first slider drag.
 */

const FADE_IN_MS = 200;
const FADE_OUT_MS = 80;

interface SoundNode {
  id: string;
  url: string;
  /** Cached decoded buffer; populated on first activation. */
  buffer: AudioBuffer | null;
  /** Currently playing source — null when volume === 0. */
  source: AudioBufferSourceNode | null;
  gain: GainNode;
  /** Last requested volume in [0, 1]. */
  volume: number;
  /** Pending decode; held to deduplicate concurrent activations. */
  loading: Promise<AudioBuffer> | null;
}

export type MixerState = Record<string, number>;

export class Mixer {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private sounds = new Map<string, SoundNode>();
  private masterVolume = 0.75;
  private playing = true;
  private listeners = new Set<() => void>();

  constructor(private readonly urlFor: (id: string) => string) {}

  /** True once the AudioContext exists. Some UI text depends on this. */
  get isReady(): boolean {
    return this.ctx !== null;
  }

  get isPlaying(): boolean {
    return this.playing;
  }

  get currentMasterVolume(): number {
    return this.masterVolume;
  }

  /** Subscribe to "anything changed" for header re-renders. Returns unsubscribe. */
  onChange(fn: () => void): () => void {
    this.listeners.add(fn);
    return () => this.listeners.delete(fn);
  }

  private notify() {
    this.listeners.forEach((fn) => fn());
  }

  /** Lazy: create the AudioContext on first user gesture. */
  private ensureContext(): AudioContext {
    if (this.ctx) return this.ctx;
    const Ctor: typeof AudioContext =
      window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    if (!Ctor) throw new Error('Web Audio API not available');
    this.ctx = new Ctor();
    this.master = this.ctx.createGain();
    this.master.gain.value = this.playing ? this.masterVolume : 0;
    this.master.connect(this.ctx.destination);
    return this.ctx;
  }

  private nodeFor(id: string): SoundNode {
    let n = this.sounds.get(id);
    if (n) return n;
    const ctx = this.ensureContext();
    const gain = ctx.createGain();
    gain.gain.value = 0;
    gain.connect(this.master!);
    n = { id, url: this.urlFor(id), buffer: null, source: null, gain, volume: 0, loading: null };
    this.sounds.set(id, n);
    return n;
  }

  private async ensureBuffer(n: SoundNode): Promise<AudioBuffer> {
    if (n.buffer) return n.buffer;
    if (n.loading) return n.loading;
    // `finally` clears `n.loading` on both success AND failure — if we left it
    // set on rejection, every subsequent call would re-await the same rejected
    // promise and the sound would be dead for the session (notably on Safari,
    // which can't decodeAudioData OGG Vorbis).
    n.loading = (async () => {
      try {
        const ctx = this.ensureContext();
        const res = await fetch(n.url);
        if (!res.ok) throw new Error(`Failed to load ${n.url}: ${res.status}`);
        const arrayBuf = await res.arrayBuffer();
        const audioBuf = await ctx.decodeAudioData(arrayBuf);
        n.buffer = audioBuf;
        return audioBuf;
      } finally {
        n.loading = null;
      }
    })();
    return n.loading;
  }

  private startSource(n: SoundNode, buf: AudioBuffer) {
    const ctx = this.ensureContext();
    const src = ctx.createBufferSource();
    src.buffer = buf;
    src.loop = true;
    src.connect(n.gain);
    src.start(0);
    n.source = src;
  }

  private stopSource(n: SoundNode) {
    if (!n.source) return;
    try {
      n.source.stop();
    } catch {
      /* Already stopped — fine. */
    }
    n.source.disconnect();
    n.source = null;
  }

  /**
   * Set per-sound volume in [0, 1]. Loads the buffer lazily on first non-zero
   * value; stops the source when set back to zero.
   */
  async setVolume(id: string, volume: number): Promise<void> {
    const n = this.nodeFor(id);
    n.volume = Math.max(0, Math.min(1, volume));
    const ctx = this.ensureContext();

    if (n.volume === 0) {
      // Fade out, then detach.
      const now = ctx.currentTime;
      n.gain.gain.cancelScheduledValues(now);
      n.gain.gain.setValueAtTime(n.gain.gain.value, now);
      n.gain.gain.linearRampToValueAtTime(0, now + FADE_OUT_MS / 1000);
      const source = n.source;
      window.setTimeout(() => {
        // Only stop if no rising edge happened during the fade.
        if (n.volume === 0 && source === n.source) this.stopSource(n);
      }, FADE_OUT_MS + 20);
      this.notify();
      return;
    }

    // Active path — ensure buffer + source, then ramp gain.
    if (!n.buffer) {
      try {
        await this.ensureBuffer(n);
      } catch (err) {
        console.error('mixer: load failed', err);
        n.volume = 0;
        this.notify();
        return;
      }
    }

    // Caller may have dropped the slider to 0 while we were awaiting the decode.
    // Without this, we'd start a silent source that never gets stopped.
    if (n.volume === 0) return;

    if (!n.source) this.startSource(n, n.buffer!);

    const nowAfter = ctx.currentTime;
    n.gain.gain.cancelScheduledValues(nowAfter);
    n.gain.gain.setValueAtTime(n.gain.gain.value, nowAfter);
    n.gain.gain.linearRampToValueAtTime(n.volume, nowAfter + FADE_IN_MS / 1000);
    this.notify();
  }

  setMasterVolume(volume: number): void {
    this.masterVolume = Math.max(0, Math.min(1, volume));
    if (!this.master || !this.ctx) return;
    if (!this.playing) return;
    const now = this.ctx.currentTime;
    this.master.gain.cancelScheduledValues(now);
    this.master.gain.setValueAtTime(this.master.gain.value, now);
    this.master.gain.linearRampToValueAtTime(this.masterVolume, now + FADE_IN_MS / 1000);
    this.notify();
  }

  setPlaying(playing: boolean): void {
    this.playing = playing;
    if (!this.master || !this.ctx) {
      this.notify();
      return;
    }
    const now = this.ctx.currentTime;
    const target = playing ? this.masterVolume : 0;
    this.master.gain.cancelScheduledValues(now);
    this.master.gain.setValueAtTime(this.master.gain.value, now);
    this.master.gain.linearRampToValueAtTime(target, now + FADE_OUT_MS / 1000);
    this.notify();
  }

  reset(): void {
    for (const n of this.sounds.values()) void this.setVolume(n.id, 0);
  }

  /** Snapshot of every non-zero volume — used for shareable URL fragments. */
  exportMix(): MixerState {
    const out: MixerState = {};
    for (const n of this.sounds.values()) {
      if (n.volume > 0) out[n.id] = Math.round(n.volume * 100) / 100;
    }
    return out;
  }

  /** Apply a mix snapshot. Volumes for ids not in the map are left untouched. */
  async applyMix(mix: MixerState): Promise<void> {
    await Promise.all(
      Object.entries(mix).map(([id, vol]) => this.setVolume(id, vol))
    );
  }

  getVolume(id: string): number {
    return this.sounds.get(id)?.volume ?? 0;
  }
}

/**
 * Encode/decode "rain:0.4,fireplace:0.6" style fragments. Simple, human-readable,
 * easy to hand-edit a URL.
 */
export function encodeMix(mix: MixerState): string {
  return Object.entries(mix)
    .filter(([, v]) => v > 0)
    .map(([id, v]) => `${id}:${v}`)
    .join(',');
}

export function decodeMix(fragment: string): MixerState {
  const out: MixerState = {};
  for (const part of fragment.split(',')) {
    const [id, raw] = part.split(':');
    const n = Number(raw);
    if (id && Number.isFinite(n) && n > 0 && n <= 1) out[id] = n;
  }
  return out;
}
