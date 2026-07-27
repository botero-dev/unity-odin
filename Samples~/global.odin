// Place this file in Assets/UnityOdin/Source
// Changes to files in UnityOdin/Source folder are monitored by Unity asset watcher.

package src

import "core:log"

import unity ".exports"

global_awake :: proc() {
    app_name := unity.GetApplicationProductName(context.allocator)
    log.info("[Odin] Awake! ", app_name)
}

global_start :: proc() {
    log.info("[Odin] Start!")
}

global_fixed_update :: proc(dt: f32) {
    // Called every physics tick — uncomment for per-frame logic:
    // log.info("[Odin] FixedUpdate dt=%f\n", dt)
}

global_update :: proc(dt, unscaled: f32) {
    // Called every frame — uncomment for per-frame logic:
    // log.info("[Odin] Update dt=%f, unscaled=%f\n", dt, unscaled)
}


global_lateUpdate :: proc(dt, unscaled: f32) {
    // Called after all Update calls — uncomment for per-frame logic:
    // log.info("[Odin] LateUpdate dt=%f, unscaled=%f\n", dt, unscaled)
}

global_destroy :: proc() {
    log.info("[Odin] Destroy!")
}


