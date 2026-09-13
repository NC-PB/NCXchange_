# Ncx.Analytics

Listeners of the virtual machine that write text tables: tool list, runtime estimate, travel limits, segment length and tool vector change, loops, channel timeline (architecture 9; virtual machine 8). Each analytic is one class that subscribes to the events it needs.

Empty so far: `Placeholder.cs` gives the project something to compile until P4-02 (`IAnalytic`, the tool list, the runtime estimate); P4-03 adds segment length and tool vector change. The tests stay in `tests/Ncx.Acceptance/` until the project has enough to deserve its own (implementation 00-method 4).

Never here: a reference to anything but `Ncx.Core`; modal state of its own (an analytic reads the Before and After of each event, architecture 2 rule 1); a change to the program or the state (listeners observe, D61); generics of our own, LINQ chains of more than two calls (code-guidelines 10.2).
