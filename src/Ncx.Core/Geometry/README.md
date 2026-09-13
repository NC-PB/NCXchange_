# Geometry

The little geometry the virtual machine needs, in `double` (architecture 4.2, D62): `Vec3.cs`, the one type of the code with operators (code-guidelines 10.2); `Plane.cs`, the working planes of `ARC` and their direction convention (D102); `Angle.cs`, degrees, turns and the sweeps of D84; and `ArcResolver.cs`, which resolves an `ARC` in its plane in the `CENTER`, `R` and `ANGLE` forms with the arc tolerance (virtual machine 3.2; D36, D84) into an `ArcResult`: an `Arc`, or an `ArcError` that says why not.

Start with `ArcResolver.cs`, then `Plane.cs`. A coordinate enters a `Vec3` once from the decimals of the model and goes back only as a rounded decimal; stored numbers stay `decimal` with their text (code-guidelines 7).

Never here: diagnostics (a problem is an `ArcError` value, which the virtual machine reports with its VM code), state, a math package (D62).
