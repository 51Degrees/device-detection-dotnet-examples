# m_takwork.1 lab

Timed runs of tak(x, y, z) on inputs the server chooses. The server knows
each input's exact call count, so a genuine engine's times are
proportional to those counts, and every call folds its arguments into a
hash that only the whole traversal can produce. `TakTiming.cs` (next to
the element) holds the input choice and the check. The gatherer test is
`m_takwork.1` in `browser-tests-v3.js`.

## The check

1. Each of the 7 inputs is timed 3 times, interleaved, in a dedicated
   worker. Each input's fastest repeat counts, because other work on the
   device only ever adds time.
2. Each input's hash must equal the server's. tak's value and call count
   come from a memo in microseconds, because the same sub problems recur
   millions of times, but the hash enters every recurrence with a
   different state, so there is no memo for it: the server does the same
   traversal, about 130 ms for the seven inputs, once per input list. This
   is checked before anything the timer decides, so a missing traversal
   fails whatever timer tick is claimed.
3. The speed is the median over the inputs of fastest time / calls. That
   is the constant per call, measured on this device in this run.
4. Each input's fastest time must be within 35% of speed x calls, plus two
   timer ticks. One input may be slower than that, never faster.
5. The speed must be between 1 and 2000 ns a call. Optimised JavaScript on
   a 2026 desktop is about 5 ns with the hash.
6. A timer tick over 5 ms, or fewer than 3 inputs expected to take 8 ticks,
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
- **No traversal.** The exact counts model with a memo's answer in place of
  the hash.
- **Exact counts.** The control: times made from the exact call counts and
  hashes from a real traversal. This passes, as it must. A forger who does
  the traversal, in native code if they like, and counts the calls can
  pass, which is why the timing is a supporting signal only.

## Results

Capture on 23 October 2026, lab VM, Windows, no GPU. Each loop run is one
fresh list of 7 inputs.

| browser | runs | pass | fail | unresolved | ns a call, median |
| --- | --- | --- | --- | --- | --- |
| Chrome 156 | 100 | 100 | 0 | 0 | 5.3 |
| Chrome Beta 157 | 40 | 40 | 0 | 0 | 5.3 |
| Edge 156 | 40 | 40 | 0 | 0 | 5.5 |
| Chrome for Testing 148 | 40 | 40 | 0 | 0 | 5.3 |
| Chrome `--headless=new` | 30 | 30 | 0 | 0 | 5.3 |
| Firefox 158 | 40 | 40 | 0 | 0 | 7.4 |
| Firefox, resistFingerprinting | 20 | 0 | 0 | 20 | - |
| Full gatherer, Chrome | 10 | 10 | 0 | 0 | - |
| Full gatherer, Firefox | 10 | 10 | 0 | 0 | - |

The same capture without the hash (`C:\bc-data\tak\runs-nohash`) passed
379 of 380 at 4.0 ns a call in Chrome and 5.5 in Firefox: the hash costs a
third. The one failure there had two inputs slowed in all three repeats
by other work on the VM. DevTools CPU throttling does not slow a
dedicated worker, so 30 throttled runs measured the same speed and
passed.

Forged answers built from these runs, 310 of each:

| forger | pass |
| --- | --- |
| instant | 0 |
| constant | 0 |
| replay | 3 |
| replay ranked by x | 2 |
| model x(x - y) | 0 |
| model 2^(x - y) | 0 |
| resend | 0 |
| no traversal | 0 |
| exact counts (control) | 310 |

Five inputs instead of seven let a randomly ordered replay pass 3.4% of
the time. Seven brings that to about 0.5% in simulation and 5 of 620
here. A first pool that allowed recursion depth 40 failed genuine Chrome
1 run in 40, through the deep inputs' higher cost per call.

What it means:

- It catches times that were not measured from the work, and answers
  made without the traversal.
- It cannot catch a real engine, however it is driven. Headless Chrome
  passes.
- A forger who does the traversal, about 50 ms in native code for a
  list, and counts the calls passes. The server issues one list per
  window, so one genuine run per window, replayed for the rest of the
  window, passes too. Per request inputs would stop that, at the cost of
  the server's traversal per request.
- A claimed coarse timer tick gives Unresolved, never a pass. The element
  could check the claimed tick against the claimed browser: Chrome is
  0.1 ms, Firefox 1 ms or 16.7 ms.
- Not measured: Safari (a 1 ms tick, like Firefox) and phones. The seven
  inputs three times are about 52 million calls, 1.5 s at 30 ns a call,
  inside the test's own 8 s limit but not the site's 3 s run budget, so a
  slow phone's answer would be dropped. Either the site gives this test
  its own budget or the targets come down for phones.
