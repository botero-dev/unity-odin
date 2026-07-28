package exports

@require import "base:runtime"

UnityPanic :: proc(prefix, message: string, loc := #caller_location) -> ! {
	panic_fatal(prefix, message, loc.procedure, loc.file_path, loc.line, loc.column)
	panic(message)
}

UNITY_DEFAULT_RANDOM_NUMBER_GENERATOR: runtime.Random_Generator : {procedure = OdnTrop_Internal_GenerateRandomNumber, data = nil}

@(private = "file")
OdnTrop_Internal_GenerateRandomNumber :: proc(data: rawptr, mode: runtime.Random_Generator_Mode, p: []byte) {
	rd := (^RandomState)(data)

	switch mode {
	case .Read:
		orSt: RandomState = ---
		if rd != nil {
			orSt = random_get_state() // save original state
			random_set_state(rd^) // apply custom state
		}

		switch len(p) {
		case size_of(u32):
			val := cast(u32)random_get_next_int()
			((^u32)(raw_data(p)))^ = val
		case size_of(u64):
			valFirst: u32 = cast(u32)random_get_next_int()
			valSecond: u32 = cast(u32)random_get_next_int()
			val: u64 = u64(valFirst) | (u64(valSecond) << 32)
			((^u64)(raw_data(p)))^ = val
		case size_of([2]u64):
			valFirstQ: u32 = cast(u32)random_get_next_int()
			valSecondQ: u32 = cast(u32)random_get_next_int()
			valThirdQ: u32 = cast(u32)random_get_next_int()
			valFourthQ: u32 = cast(u32)random_get_next_int()
			valFirstH: u64 = u64(valFirstQ) | (u64(valSecondQ) << 32)
			valSecondH: u64 = u64(valThirdQ) | (u64(valFourthQ) << 32)
			val: [2]u64 = {valFirstH, valSecondH}
			((^[2]u64)(raw_data(p)))^ = val
		case:
			pos := i8(0)
			val := u32(0)
			for &v in p {
				if pos == 0 {
					val = cast(u32)random_get_next_int()
					pos = 3
				}
				v = byte(val)
				val >>= 8
				pos -= 1
			}
		}

		if rd != nil {
			rd^ = random_get_state() // store new state for the custom one
			random_set_state(orSt) // restore original state for the default one
		}
	case .Reset:
		seed: i32 = 0
		switch len(p) {
		case 0:
			seed = 0
		case 1:
			seed = i32(p[0])
		case 2:
			seed = i32(p[0]) | i32(p[1]) << 8
		case 3:
			seed = i32(p[0]) | i32(p[1]) << 8 | i32(p[2]) << 16
		case:
			seed = i32(p[0]) | i32(p[1]) << 8 | i32(p[2]) << 16 | i32(p[3]) << 24
		}

		orSt: RandomState = ---
		if rd != nil {
			orSt = random_get_state() // save original state
			random_set_state(rd^) // apply custom state
		}

		random_init_state(seed)
		if rd != nil {
			rd^ = random_get_state() // store new state for the custom one
			random_set_state(orSt) // restore original state for the default one
		}

	case .Query_Info:
		if len(p) != size_of(runtime.Random_Generator_Query_Info) {
			return
		}

		info := cast(^runtime.Random_Generator_Query_Info)(raw_data(p))
		info^ = {.Uniform, .Resettable}
	}
}

InstantiateObject :: proc {
	object_instantiate_without_transform,
	object_instantiate_with_transform,
}

GetTransform :: proc {
	game_object_get_transform_from,
	component_get_transform_from,
}

GetGameObjectLayer :: proc(go: GameObject) -> GameObjectLayer {
	return GameObjectLayer(u8(game_object_get_layer(go)))
}

SetGameObjectLayer :: proc(go: GameObject, layer: GameObjectLayer) {
	game_object_set_layer(go, i32(u8(layer)))
}

IsEnabled :: proc {
    component_is_behaviour_enabled,
    renderer_is_enabled,
    collider_is_enabled,
}
