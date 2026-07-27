package exports

import unity "../exports"

import "base:runtime"

@(thread_local)
G_OdnTrop_Internal_Ctx: runtime.Context
@(thread_local)
G_OdnTrop_Internal_CtxNesting: uint

CreateUnityContext :: proc() -> runtime.Context {
	return {
		allocator = unity.UNITY_MAIN_ALLOCATOR,
		temp_allocator = unity.UNITY_MAIN_TEMP_ALLOCATOR,
		assertion_failure_proc = unity.UnityPanic,
		logger = unity.UNITY_MAIN_LOGGER,
		random_generator = unity.UNITY_DEFAULT_RANDOM_NUMBER_GENERATOR,
	}
}
