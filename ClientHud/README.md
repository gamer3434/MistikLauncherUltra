# Mistik HUD

Original client-only Fabric **Minecraft 1.21.1** HUD. No Lunar code, branding, or assets are included.

Press **Right Shift** while in a world to open the editor. Toggle FPS, left/right CPS, ping, coordinates, keystrokes, and armor independently. Choose **Move HUD / Yerleşim** to drag boxes; use **Scale +/-** or **Reset** in the editor. Positions, scale, and switches persist in `config/mistik-hud.json`. HUD stays hidden with Minecraft's F1 toggle.

Keystrokes use the configured movement bindings and show the familiar W / A S D / LMB RMB / space layout. Mouse CPS counts physical press events, including clicks between rendered frames. Armor displays remaining durability as a percentage. Text values refresh at 10 Hz; rendering uses cached text.

Build with Java 21 and Gradle 8.10.2: `gradle build`. Output: `build/libs/mistik-hud-1.0.0.jar`. Requires Fabric Loader >=0.16.0 and Fabric API. Unit tests cover configuration recovery, persistence, bounds, and the one-second bounded click counter.

The click history is capped at 128 presses per mouse button per second. This is a display mod and does not implement Lunar Client's unrelated features.
