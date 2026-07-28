package interop

import "base:runtime"
import exports "../.exports"

engine_bindings_imports__get_main_odn_allocator :: proc() -> runtime.Allocator {
	return exports.UNITY_MAIN_ALLOCATOR
}

engine_bindings_imports__get_temp_odn_allocator :: proc() -> runtime.Allocator {
	return exports.UNITY_MAIN_TEMP_ALLOCATOR
}

engine_bindings_imports__allocate_using_odn_allocator :: proc(size: i32, alignment: i32, count: i32, allocator: runtime.Allocator) -> []u8 {
	x, _ := runtime.mem_alloc_bytes(size = int(size * count), alignment = int(alignment), allocator = allocator)
	return x
}

engine_bindings_imports__free_using_odn_allocator :: proc(ptr: []u8, allocator: runtime.Allocator) {
	runtime.mem_free_bytes(bytes = ptr, allocator = allocator)
}
