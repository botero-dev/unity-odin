package main

import "core:odin/parser"
import "core:odin/ast"
import "core:odin/tokenizer"
import "core:fmt"
import "core:os"
import "core:strings"

ProcInfo :: struct {
	class_name:  string,
	method_name: string,
	odin_name:   string,
	params:      []ParamInfo,
	return_type: string,
	source_file: string,
	source_line: int,
}

ParamInfo :: struct {
	name: string,
	type: string,
}

main :: proc() {
	args := os.args[1:]

	input_dir := "."
	output_dir := ""
	defer delete(output_dir)

	if len(args) >= 1 {
		input_dir = args[0]
	}
	if len(args) >= 2 {
		output_dir = strings.clone(args[1])
	}

	if output_dir == "" {
		output_dir = fmt.tprintf("%s/../Generated", input_dir)
	}

	fmt.printf("Odin2Cs: Scanning %s -> %s\n", input_dir, output_dir)

	pkg, ok := parser.parse_package_from_path(input_dir)
	if !ok {
		fmt.eprintf("Error: failed to parse package at %s\n", input_dir)
		os.exit(1)
	}

	// Generate one .cs file per .odin source file
	os.make_directory(output_dir)
	total_files := 0

	for path, file in pkg.files {
		// Extract filename from path
		filename := path
		last_slash := -1
		for i := len(path) - 1; i >= 0; i -= 1 {
			if path[i] == '/' {
				last_slash = i
				break
			}
		}
		if last_slash >= 0 {
			filename = path[last_slash+1:]
		}

		// Skip generated odntrop_* files
		if strings.has_prefix(filename, "odntrop_") {
			continue
		}

		// Collect procs from this file only
		file_procs := make([dynamic]ProcInfo)
		for decl in file.decls {
			process_node(decl, &file_procs, path)
		}

		if len(file_procs) == 0 {
			continue
		}

		// Derive output filename from source file (strip .odin, convert to PascalCase)
		base_name := filename
		if strings.has_suffix(base_name, ".odin") {
			base_name = base_name[:len(base_name)-5]
		}
		file_class_name := to_pascal_case(base_name)

		// Group procs by class_name prefix within this file
		class_names := make([dynamic]string)
		class_methods := make([dynamic][dynamic]ProcInfo)
		for p in file_procs {
			found := -1
			for name, i in class_names {
				if name == p.class_name {
					found = i
					break
				}
			}
			if found < 0 {
				append(&class_names, strings.clone(p.class_name))
				found = len(class_names) - 1
				append(&class_methods, [dynamic]ProcInfo{})
			}
			append(&class_methods[found], p)
		}

		// Generate C# file with multiple classes
		cs_code := generate_csharp_file(file_class_name, class_names, class_methods)
		output_path := fmt.tprintf("%s/%s.g.cs", output_dir, file_class_name)
		err := os.write_entire_file(output_path, transmute([]byte)cs_code)
		if err == nil {
			fmt.printf("  Generated: %s (%d methods in %d classes)\n", output_path, len(file_procs), len(class_names))
			total_files += 1
		} else {
			fmt.eprintf("  Error writing %s: %v\n", output_path, err)
		}
	}

	if total_files == 0 {
		fmt.printf("No binding procs found (no functions matching ClassName_MethodName pattern)\n")
	} else {
		fmt.printf("Done.\n")
	}
}

process_node :: proc(stmt: ^ast.Stmt, procs: ^[dynamic]ProcInfo, source_path: string) {
	#partial switch s in stmt.derived_stmt {
	case ^ast.Value_Decl:
		process_value_decl(s, procs, source_path)
	case:
	}
}

process_value_decl :: proc(vd: ^ast.Value_Decl, procs: ^[dynamic]ProcInfo, source_path: string) {
	if len(vd.names) == 0 || len(vd.values) == 0 {
		return
	}

	name_ident: ^ast.Ident
	#partial switch id in vd.names[0].derived_expr {
	case ^ast.Ident:
		name_ident = id
	}
	if name_ident == nil {
		return
	}

	full_name := name_ident.name

	// Check if first value is a Proc_Lit
	proc_lit: ^ast.Proc_Lit
	#partial switch lit in vd.values[0].derived_expr {
	case ^ast.Proc_Lit:
		proc_lit = lit
	}
	if proc_lit == nil || proc_lit.type == nil {
		return
	}

	proc_type: ^ast.Proc_Type
	#partial switch pt in proc_lit.type.derived_expr {
	case ^ast.Proc_Type:
		proc_type = pt
	}
	if proc_type == nil {
		return
	}

	// Check for ClassName_MethodName pattern (at least one underscore)
	// Split at the FIRST underscore: class_method_name -> class / method_name
	first_underscore := -1
	for i := 0; i < len(full_name); i += 1 {
		if full_name[i] == '_' {
			first_underscore = i
			break
		}
	}

	if first_underscore <= 0 {
		return // No underscore or starts with underscore - not a binding proc
	}

	class_name := full_name[:first_underscore]
	method_name := full_name[first_underscore + 1:]

	// Skip odntrop_ prefixed ones (internal)
	if strings.has_prefix(class_name, "odntrop") {
		return
	}

	// Convert snake_case to PascalCase for C# naming conventions
	class_name_pascal := to_pascal_case(class_name)
	method_name_pascal := to_pascal_case(method_name)

	info := ProcInfo{
		class_name  = strings.clone(class_name_pascal),
		method_name = strings.clone(method_name_pascal),
		odin_name   = strings.clone(full_name),
		return_type = extract_return_type(proc_type),
		source_file = strings.clone(source_path),
		source_line = proc_lit.pos.line,
	}

	// Extract params
	if proc_type.params != nil {
		info.params = make([]ParamInfo, len(proc_type.params.list))
		for f, i in proc_type.params.list {
			param_name := extract_param_name(f)
			param_type := extract_type_string(f.type)
			info.params[i] = ParamInfo{
				name = strings.clone(param_name),
				type = strings.clone(param_type),
			}
		}
	}

	append(procs, info)
}

extract_param_name :: proc(field: ^ast.Field) -> string {
	if len(field.names) > 0 {
		#partial switch id in field.names[0].derived_expr {
		case ^ast.Ident:
			return id.name
		}
	}
	return ""
}

extract_type_string :: proc(type_expr: ^ast.Expr) -> string {
	if type_expr == nil {
		return "void"
	}

	#partial switch alt in type_expr.derived_expr {
	case ^ast.Ident:
		return alt.name
	case ^ast.Pointer_Type:
		elem := extract_type_string(alt.elem)
		return fmt.tprintf("^%s", elem)
	case ^ast.Array_Type:
		elem := extract_type_string(alt.elem)
		return fmt.tprintf("[]%s", elem)
	case ^ast.Dynamic_Array_Type:
		elem := extract_type_string(alt.elem)
		return fmt.tprintf("[dynamic]%s", elem)
	case ^ast.Selector_Expr:
		// Strip package prefix — only use the type name (e.g., "unity.TestComponent" -> "TestComponent")
		return alt.field.name
	case ^ast.Proc_Type:
		if alt.results == nil || len(alt.results.list) == 0 {
			return "proc"
		}
		ret := extract_type_string(alt.results.list[0].type)
		return fmt.tprintf("proc -> %s", ret)
	}

	return "<type>"
}

extract_return_type :: proc(pt: ^ast.Proc_Type) -> string {
	if pt.results == nil || len(pt.results.list) == 0 {
		return ""
	}

	if len(pt.results.list) == 1 {
		return extract_type_string(pt.results.list[0].type)
	}

	// Multiple return values - use tuple syntax
	b := strings.builder_make()
	strings.write_string(&b, "(")
	for field, i in pt.results.list {
		if i > 0 {
			strings.write_string(&b, ", ")
		}
		strings.write_string(&b, extract_type_string(field.type))
	}
	strings.write_string(&b, ")")
	return strings.to_string(b)
}

// Convert snake_case to PascalCase (e.g., "navmesh_create" -> "NavmeshCreate")
to_pascal_case :: proc(s: string) -> string {
	b := strings.builder_make()
	capitalize_next := true
	for c in s {
		if c == '_' {
			capitalize_next = true
			continue
		}
		if capitalize_next {
			if c >= 'a' && c <= 'z' {
				strings.write_byte(&b, u8(c - 32))
			} else {
				strings.write_byte(&b, u8(c))
			}
			capitalize_next = false
		} else {
			strings.write_byte(&b, u8(c))
		}
	}
	return strings.to_string(b)
}

// Type mapping: Odin -> C# interop types
map_type :: proc(odin_type: string) -> string {
	switch odin_type {
	case "i8":    return "sbyte"
	case "i16":   return "short"
	case "i32":   return "int"
	case "i64":   return "long"
	case "u8":    return "byte"
	case "u16":   return "ushort"
	case "u32":   return "uint"
	case "u64":   return "ulong"
	case "f32":   return "float"
	case "f64":   return "double"
	case "bool":  return "bool"
	case "int":   return "long"
	case "uint":  return "ulong"
	case "string": return "string"
	case "rawptr": return "void*"
	case "rawslice": return "RawSlice"
	case "rawdynamicarray": return "RawDynamicArray"
	case "rawobjecthandle": return "RawObjectHandle"
	case: return odin_type // pass through for custom types (TestComponent, etc.)
	}
}

generate_csharp_file :: proc(base_name: string, class_names: [dynamic]string, class_methods: [dynamic][dynamic]ProcInfo) -> string {
	b := strings.builder_make()

	strings.write_string(&b, "// THIS IS A GENERATED FILE - DO NOT MODIFY!\n")
	fmt.sbprintf(&b, "// Generated by Odin2Cs from %s.odin\n\n", base_name)
	strings.write_string(&b, "using OdinInterop;\n")
	strings.write_string(&b, "using UnityEngine;\n\n")

	for i in 0..<len(class_names) {
		class_name := class_names[i]
		methods := class_methods[i]

		fmt.sbprintf(&b, "[OdinImport]\n")
		fmt.sbprintf(&b, "internal static partial class %s\n", class_name)
		strings.write_string(&b, "{\n")

		for m in methods {
			return_type := m.return_type
			if return_type == "" {
				return_type = "void"
			} else {
				return_type = map_type(return_type)
			}

			// Emit source link
			if m.source_line > 0 {
				fmt.sbprintf(&b, "\t// Source: file://%s#L%d\n", m.source_file, m.source_line)
			}

			// Emit ForeignDecl attribute with Odin name and original type info
			strings.write_string(&b, "\t[ForeignDecl(OdinName = \"")
			strings.write_string(&b, m.odin_name)
			strings.write_string(&b, "\"")

			if m.return_type != "" {
				fmt.sbprintf(&b, ", ReturnType = \"%s\"", m.return_type)
			}

			if len(m.params) > 0 {
				strings.write_string(&b, ", ParamTypes = new[] { ")
				for param, j in m.params {
					if j > 0 {
						strings.write_string(&b, ", ")
					}
					fmt.sbprintf(&b, "\"%s\"", param.type)
				}
				strings.write_string(&b, " }")
			}

			strings.write_string(&b, ")]\n")
			fmt.sbprintf(&b, "\tpublic static partial %s %s(", return_type, m.method_name)

			for param, j in m.params {
				if j > 0 {
					strings.write_string(&b, ", ")
				}
				cs_type := map_type(param.type)
				fmt.sbprintf(&b, "%s %s", cs_type, param.name)
			}

			strings.write_string(&b, ");\n\n")
		}

		strings.write_string(&b, "}\n")
		if i < len(class_names) - 1 {
			strings.write_string(&b, "\n")
		}
	}

	return strings.to_string(b)
}
