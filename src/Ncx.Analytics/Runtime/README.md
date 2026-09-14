# Runtime

The runtime estimate of virtual machine 8 and D64 (implementation 14, P4-02): per motion a trapezoidal velocity profile from the machine file, the dwells, the plunges and dwells of the expanded cycles, the spindle starts and stops; totals per tool, per `SECTION` and per channel. It is an estimate, and its report says so and lists every value of the machine file it used, so that the file is corrected rather than the code.

Start with `RuntimeAnalytic.cs`, the listener with its totals (`RuntimeAnalytic.Report.cs` writes them), then `RuntimeEstimator.cs`, the model as a stream: every event in, every time charged out (`TimedStep.cs`), with one motion kept back (`PendingMotion.cs`) whose exit speed waits for the next motion in continuous mode. The pieces it uses:

- `MotionProfile.cs`: the time of one motion under the trapezoidal profile, deceleration equal to acceleration (D64).
- `MovingAxes.cs`: the axes a motion moves (on an arc both plane axes, a full circle included, virtual machine 3.2), and the smallest rapid, max_feed and acceleration among them (virtual machine 8); rotary travel belongs to the kinematics module (virtual machine 9).
- `CommandedFeed.cs` and `SpindleSpeed.cs`: the feed in mm/min, per minute or per revolution times the rpm, under CSS from VC and the X radius capped by RPM_MAX.
- `MachineDynamics.cs`: `[[axis]]` rapid, max_feed, acceleration, `[dynamics]` block_time, path_mode, corner_speed (machine-config 4), accel_time of the spindle tables (machine-config 5); without `[dynamics]` the estimate is distance over feed.

Never here: a value of a machine in the code; every number comes from the machine file.
