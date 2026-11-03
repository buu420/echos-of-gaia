# Manual navigation

Guidance follows the current walking run to its next meaningful bend. Small
sideways drift keeps that instruction when the forward segment is clear. For
larger drift it says, for example, "Correction: left 3. Then up 3." The correction
returns to the nearest safe point at the player's current progress on the run,
rather than an earlier path node. If rejoining is blocked, the native walking
geometry is used to find a fresh route.

Counts are approximate walking distances, not grid squares or key presses.
The same unit applies to keyboard, D-pad and stick, whether walking or running.
Progress comes from the character's actual map position; running consumes those
units faster. Changing the control angle does not shorten the measured path.
Human turn tolerance uses the character's collision radius, and a corner can
advance only when the next segment is clear. Tight corners may still need a
small final adjustment. Actual interaction/Confirm remains under player control.

Releasing the stick retains the game's last actual digital/stick movement
choice. Scrolling a camera does not rotate the route. When the game changes
the control angle, the spoken key direction uses that current angle. Thus an
earlier right 3 can be undone by left 3 only while those input axes remain the
same. Native controls are never remapped or frozen by guidance.

Equivalent path refreshes and harmless exit reference-point changes stay
quiet. A new turn, necessary correction, blocked route, or changed target still
updates guidance. Status and Repeat remain available. Existing reminder timing,
automatic walking, menus, dialogue and map/battle interruption rules remain in
effect.
