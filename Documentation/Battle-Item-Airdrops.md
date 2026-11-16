# Battle item team supplies

An equipped battle item starts a recurring supply schedule. The first crate can land at 2:00 match time, then its slot is checked every two minutes. A crate that has not been collected remains in place and prevents another crate for that slot. If the slot is empty at a scheduled tick, another crate is created. A blocked landing remains pending and retries until a safe position is available. Pauses stop the match timer.

The equipped inventory item is consumed once when the first supply successfully starts. Later supplies of the captured item during that same match do not consume additional inventory. No item is consumed if the match ends before the first successful drop. Bots do not schedule inventory items, although an allied bot or player can collect a landed crate.

In network matches the host or dedicated server owns the pickup, determines its landing position and applies its effect. It periodically publishes the state of each slot; clients only show replicas and consume their own equipped item once after the first server-confirmed supply. Server and client builds must both include this protocol. The server can create an authoritative pickup even when rendering shaders are absent.

Each human player has a separate team landing lane. Crates descend for four seconds and cannot be collected in the air. They persist until a living allied hero reaches them, or the match ends. Regen restores 20% maximum health; Aegis grants a 20% maximum-health shield for five seconds; Velocity grants 10% movement speed for five seconds; Team Heal restores 20 HP to living allied heroes.

The regression fixture exercises the real scheduler and snapshot handlers with controlled Unity/network substitutes. It does not replace Unity play-mode, Android-device or live multiplayer tests.
