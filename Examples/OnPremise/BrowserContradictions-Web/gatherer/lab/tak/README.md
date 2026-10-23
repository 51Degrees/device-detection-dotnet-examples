# m_takwork.1 lab

Timed runs of tak(x, y, z) on inputs the server chooses. The server knows
each input's exact call count, so a genuine engine's times are
proportional to those counts. `TakTiming.cs` (next to the element) holds
the input choice and the check. The gatherer test is `m_takwork.1` in
`browser-tests-v3.js`.

## The check

1. Each of the 7 inputs is timed 3 times, interleaved, in a dedicated
   worker. Each input's fastest repeat counts, because other work on the
   device only ever adds time.
2. The speed is the median over the inputs of fastest time / calls. That
   is the constant per call, measured on this device in this run.
3. Each input's fastest time must be within 35% of speed x calls, plus two
   timer ticks. One input may be slower than that, never faster.
4. The speed must be between 1 and 2000 ns a call. Optimised JavaScript on
   a 2026 desktop is about 4 ns.
5. A timer tick over 5 ms, or fewer than 3 inputs expected to take 8 ticks,
   makes the answer Unresolved. Firefox with resistFingerprinting rounds
   `performance.now` to 16.7 ms, too coarse to judge. Firefox's normal 1 ms
   tick resolves the larger inputs.

Inputs are limited to recursion depth 30. Deeper inputs cost up to a
quarter more per call in V8.

## Running it

```bash
cd check
dotnet run -c Release -- seeds 200 ../seeds.json
py -I ../taklab.py 5195 C:\bc-data\tak\runs
```

Then drive browsers with `../win-run.js` against
`http://localhost:5195/loop?from=0&n=40`, which runs the test alone for 40
lists in one page. `/full?i=3` runs the whole gatherer once with list 3,
so the test competes with every other test. `C:\bc-data\tak\capture.sh` is
the capture used for the results below.

```bash
dotnet run -c Release -- verify C:\bc-data\tak\runs
dotnet run -c Release -- forge C:\bc-data\tak\runs
```

`forge` builds bad actor answers for new inputs from the genuine runs.
Each forger knows the right tak results, and some know the genuine times
of other inputs.

- **Instant.** A memoised or native tak with a real clock.
- **Constant.** One plausible time for every input.
- **Replay.** Another run's times, in its order, or ranked by x.
- **Model.** Times from a guess at the cost, either x(x - y) or 2^(x - y).
- **Resend.** A genuine value sent again for new inputs.
- **Exact counts.** The control: times made from the exact call counts.
  This passes, as it must. A forger who can count calls exactly can pass,
  which is why the timing is a supporting signal only.

## Results

Capture on 23 October 2026, lab VM, Windows, no GPU. Each loop run is one
fresh list of 7 inputs.

| browser | runs | pass | fail | unresolved | ns a call, median |
| --- | --- | --- | --- | --- | --- |
| Chrome 156 | 100 | 100 | 0 | 0 | 4.0 |
| Chrome Beta 157 | 40 | 39 | 1 | 0 | 4.0 |
| Edge 156 | 40 | 40 | 0 | 0 | 4.0 |
| Chrome for Testing 148 | 40 | 40 | 0 | 0 | 4.0 |
| Chrome, 4x CPU throttling | 30 | 30 | 0 | 0 | 4.1 |
| Chrome `--headless=new` | 30 | 30 | 0 | 0 | 4.1 |
| Firefox 158 | 40 | 40 | 0 | 0 | 5.5 |
| Firefox, resistFingerprinting | 20 | 0 | 0 | 20 | - |
| Full gatherer, Chrome | 10 | 10 | 0 | 0 | - |
| Full gatherer, Firefox | 10 | 10 | 0 | 0 | - |

The one failure had two inputs slowed in all three repeats by other work
on the VM. DevTools CPU throttling does not slow a dedicated worker, so the
throttled runs measure the same speed.

Forged answers built from these runs, 339 of each:

| forger | pass |
| --- | --- |
| instant | 0 |
| constant | 0 |
| replay | 0 |
| replay ranked by x | 2 |
| model x(x - y) | 0 |
| model 2^(x - y) | 0 |
| resend | 0 |
| exact counts (control) | 339 |

Five inputs instead of seven let a randomly ordered replay pass 3.4% of
the time. Seven brings that to about 0.5% in simulation and 0 of 339 here.
A first pool that allowed recursion depth 40 failed genuine Chrome 1 run
in 40, through the deep inputs' higher cost per call.

What it means:

- It catches times that were not measured from the work.
- It cannot catch a real engine, however it is driven. Headless Chrome
  passes.
- A forger who computes the call counts, which is cheap, passes. So does
  one who sends a coarse timer tick, which makes the answer Unresolved.
  The element should only count a pass as evidence, and could check the
  claimed tick against the claimed browser: Chrome is 0.1 ms, Firefox 1 ms
  or 16.7 ms.

