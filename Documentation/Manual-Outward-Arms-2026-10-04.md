# Manual outward arm correction

The user's screenshot confirms `MIXAMO Standing Idle v2 ACTIVE` while the visible
Zhaev guard is still crowded around the torso. A manual opening is now applied to
the actual shoulder-to-hand direction after the baseline guard is rebuilt.

Each arm turns 50 degrees toward its own outer side. If it is already less than
50 degrees away from fully outward, it stops there instead of overshooting. Only
the shoulder is rotated; its elbow, hand and attached weapon follow as one chain.
The angle does not accumulate each frame or apply 50 degrees at every joint.

Sivra/Nyra is excluded. Auren/Kaelon's left shield arm is excluded. The corrected
guard is captured before basic attacks, so attacks recover to this new baseline;
ability poses retain their existing priority. Menu previews use the same path.

The activation log is now `Standing Idle v3 + manual 50-degree outward arms ACTIVE`.
The existing live pose check compares against the corrected pose. C# syntax and
numerical left/right opening checks were performed outside Unity. The supplied
Unity regression checks still require execution in the user's editor; live mesh
and weapon clearance cannot be confirmed here.

## Straight elbows requested afterwards

The baseline now also aligns each forearm with its upper arm, forming one straight
shoulder-elbow-hand line. Only the elbow rotates; hand and weapon follow and no
bone length or scale is modified. Parent-space correction accounts for nonuniform
hero scaling. It runs after outward opening and before attack recovery is captured.
Attacks and abilities may still bend their elbows during their normal actions.
Sivra and Auren's left shield arm remain excluded. The activation message is v4;
the live check now reports actual elbow bend (zero means straight).
