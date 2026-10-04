let context;
let tail = 0;
export async function play(sameAsLast, inList) {
    context ??= new (window.AudioContext || window.webkitAudioContext)();
    await context.resume();
    if (context.state !== "running") throw new Error("Audio unavailable");
    let time = Math.max(context.currentTime + 0.01, tail);
    const tone = (frequency, duration) => {
        const oscillator = context.createOscillator();
        const gain = context.createGain();
        oscillator.frequency.value = frequency;
        gain.gain.setValueAtTime(0, time);
        gain.gain.linearRampToValueAtTime(0.25, time + 0.01);
        gain.gain.setValueAtTime(0.25, time + duration - 0.02);
        gain.gain.linearRampToValueAtTime(0, time + duration);
        oscillator.connect(gain);
        gain.connect(context.destination);
        oscillator.onended = () => { oscillator.disconnect(); gain.disconnect(); };
        oscillator.start(time);
        oscillator.stop(time + duration);
        time += duration + 0.07;
    };
    if (sameAsLast) { tone(880, 0.12); tone(880, 0.12); }
    if (inList) { for (let i = 0; i < 3; i++) { tone(520, 0.20); tone(1250, 0.20); } }
    tail = time;
}
