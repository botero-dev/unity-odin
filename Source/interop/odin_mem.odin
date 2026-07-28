package interop

import "base:runtime"
import exports "../.exports"

odin_mem__get_allocator :: proc() -> runtime.Allocator {
	return exports.UNITY_MAIN_ALLOCATOR
}

odin_mem__get_temp_allocator :: proc() -> runtime.Allocator {
	return exports.UNITY_MAIN_TEMP_ALLOCATOR
}

odin_mem__allocate_with_allocator :: proc(size: i32, alignment: i32, count: i32, allocator: runtime.Allocator) -> []u8 {
	x, _ := runtime.mem_alloc_bytes(size = int(size * count), alignment = int(alignment), allocator = allocator)
	return x
}

odin_mem__free_with_allocator :: proc(ptr: []u8, allocator: runtime.Allocator) {
	runtime.mem_free_bytes(bytes = ptr, allocator = allocator)
}
