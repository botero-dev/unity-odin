// Place this file in Assets/Odin/ (the root, not in exports/ or imports/).
//
// Changes to files in Odin source folder are monitored by Unity Asset Watcher

package src

import "core:fmt"

OnGlobalAwake :: proc() {
    fmt.println("[Odin] Awake!")
}

OnGlobalStart :: proc() {
    fmt.println("[Odin] Start!")
}

OnGlobalFixedUpdate :: proc(dt: f32) {
    // Called every physics tick — uncomment for per-frame logic:
    // fmt.printf("[Odin] FixedUpdate dt=%f\n", dt)
}

OnGlobalUpdate :: proc(dt, unscaled: f32) {
    // Called every frame — uncomment for per-frame logic:
    // fmt.printf("[Odin] Update dt=%f, unscaled=%f\n", dt, unscaled)
}

OnGlobalLateUpdate :: proc(dt, unscaled: f32) {
    // Called after all Update calls — uncomment for per-frame logic:
    // fmt.printf("[Odin] LateUpdate dt=%f, unscaled=%f\n", dt, unscaled)
}

OnGlobalDestroy :: proc() {
    fmt.println("[Odin] Destroy!")
}
