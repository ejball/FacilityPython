using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Facility.Definition;
using Facility.Definition.CodeGen;
using Facility.Definition.Http;

namespace Facility.CodeGen.Python
{
	internal sealed class CodeTemplateGlobals
	{
		public CodeTemplateGlobals(PythonGenerator generator, ServiceInfo serviceInfo, HttpServiceInfo? httpServiceInfo)
		{
			Service = serviceInfo;
			HttpService = httpServiceInfo;
			CodeGenCommentText = CodeGenUtility.GetCodeGenComment(generator.GeneratorName ?? "");
		}

		public ServiceInfo Service { get; }

		public HttpServiceInfo? HttpService { get; }

		public string CodeGenCommentText { get; }

		public HttpElementInfo? GetHttp(ServiceElementInfo methodInfo) =>
			HttpService?.Methods.FirstOrDefault(x => x.ServiceMethod == methodInfo);

		public ServiceTypeInfo? GetFieldType(ServiceFieldInfo field) => Service.GetFieldType(field);

		public static string RenderFieldTypeClass(ServiceTypeInfo typeInfo) =>
			typeInfo.Kind switch
			{
				ServiceTypeKind.String => "str",
				ServiceTypeKind.Boolean => "bool",
				ServiceTypeKind.Double => "float",
				ServiceTypeKind.Int32 => "int",
				ServiceTypeKind.Int64 => "int",
				ServiceTypeKind.Decimal => "Decimal",
				ServiceTypeKind.Bytes => "bytes",
				ServiceTypeKind.Object => "object",
				ServiceTypeKind.Error => "facility.Error",
				ServiceTypeKind.Dto => typeInfo.Dto!.Name,
				ServiceTypeKind.Enum => typeInfo.Enum!.Name,
				ServiceTypeKind.Result => $"facility.Result",
				ServiceTypeKind.Array => $"list",
				ServiceTypeKind.Map => $"dict",
				_ => throw new ArgumentException("Type kind out of range.", nameof(typeInfo)),
			};

		public static string RenderFieldTypeDeclaration(ServiceTypeInfo typeInfo) =>
			typeInfo.Kind switch
			{
				ServiceTypeKind.String => "str",
				ServiceTypeKind.Boolean => "bool",
				ServiceTypeKind.Double => "float",
				ServiceTypeKind.Int32 => "int",
				ServiceTypeKind.Int64 => "int",
				ServiceTypeKind.Decimal => "Decimal",
				ServiceTypeKind.Bytes => "bytes",
				ServiceTypeKind.Object => "object",
				ServiceTypeKind.Error => "facility.Error",
				ServiceTypeKind.Dto => typeInfo.Dto!.Name,
				ServiceTypeKind.Enum => typeInfo.Enum!.Name,
				ServiceTypeKind.Result => "facility.Result",  // TODO: parameterize
				ServiceTypeKind.Array => $"List[{RenderFieldTypeDeclaration(typeInfo.ValueType!)}]",
				ServiceTypeKind.Map => $"Dict[str,{RenderFieldTypeDeclaration(typeInfo.ValueType!)}]",
				_ => throw new ArgumentException("Type kind out of range.", nameof(typeInfo)),
			};

		public static string RenderFieldType(ServiceTypeInfo typeInfo) =>
			typeInfo.Kind switch
			{
				ServiceTypeKind.String => "string",
				ServiceTypeKind.Boolean => "boolean",
				ServiceTypeKind.Double => "double",
				ServiceTypeKind.Int32 => "int32",
				ServiceTypeKind.Int64 => "int64",
				ServiceTypeKind.Decimal => "decimal",
				ServiceTypeKind.Bytes => "bytes",
				ServiceTypeKind.Object => "object",
				ServiceTypeKind.Error => "error",
				ServiceTypeKind.Dto => $"[{typeInfo.Dto!.Name}]({typeInfo.Dto.Name}.md)",
				ServiceTypeKind.Enum => $"[{typeInfo.Enum!.Name}]({typeInfo.Enum.Name}.md)",
				ServiceTypeKind.Result => $"result<{RenderFieldType(typeInfo.ValueType!)}>",
				ServiceTypeKind.Array => $"{RenderFieldType(typeInfo.ValueType!)}[]",
				ServiceTypeKind.Map => $"map<{RenderFieldType(typeInfo.ValueType!)}>",
				_ => throw new ArgumentException("Type kind out of range.", nameof(typeInfo)),
			};

		public static string RenderFieldTypeAsJsonValue(ServiceTypeInfo typeInfo) =>
			typeInfo.Kind switch
			{
				ServiceTypeKind.String => "\"(string)\"",
				ServiceTypeKind.Boolean => "(true|false)",
				ServiceTypeKind.Double => "(number)",
				ServiceTypeKind.Decimal => "(number)",
				ServiceTypeKind.Int32 => "(integer)",
				ServiceTypeKind.Int64 => "(integer)",
				ServiceTypeKind.Bytes => "\"(base64)\"",
				ServiceTypeKind.Object => "{ ... }",
				ServiceTypeKind.Error => "{ \"code\": ... }",
				ServiceTypeKind.Dto => RenderDtoAsJsonValue(typeInfo.Dto!),
				ServiceTypeKind.Enum => RenderEnumAsJsonValue(typeInfo.Enum!),
				ServiceTypeKind.Result => $"{{ \"value\": {RenderFieldTypeAsJsonValue(typeInfo.ValueType!)} | \"error\": {{ \"code\": ... }} }}",
				ServiceTypeKind.Array => $"[ {RenderFieldTypeAsJsonValue(typeInfo.ValueType!)}, ... ]",
				ServiceTypeKind.Map => $"{{ \"...\": {RenderFieldTypeAsJsonValue(typeInfo.ValueType!)}, ... }}",
				_ => throw new ArgumentException("Type kind out of range.", nameof(typeInfo)),
			};

		public IEnumerable WhereNotObsolete(IEnumerable items)
		{
			foreach (var item in items)
			{
				if (item is ServiceElementWithAttributesInfo withAttributes)
				{
					if (!withAttributes.IsObsolete)
						yield return item;
				}
				else if (item is HttpMethodInfo httpMethod)
				{
					if (!httpMethod.ServiceMethod.IsObsolete)
						yield return item;
				}
				else if (item is HttpFieldInfo httpField)
				{
					if (!httpField.ServiceField.IsObsolete)
						yield return item;
				}
				else
				{
					throw new InvalidOperationException("WhereNotObsolete: Unsupported type " + item.GetType().Name);
				}
			}
		}

		public static string StatusCodePhrase(HttpStatusCode statusCode)
		{
			s_reasonPhrases.TryGetValue((int) statusCode, out var reasonPhrase);
			return reasonPhrase;
		}

		public static string SnakeCase(string text)
		{
			text = Regex.Replace(text, @"(\p{Ll})(\p{Lu})", @"$1_$2").ToLowerInvariant() +
				(s_pythonReserved.Contains(text) ? "_" : "");
			return text;
		}

		private static string RenderDtoAsJsonValue(ServiceDtoInfo dtoInfo)
		{
			var visibleFields = dtoInfo.Fields.Where(x => !x.IsObsolete).ToList();
			return visibleFields.Count == 0 ? "{}" : $"{{ \"{visibleFields[0].Name}\": ... }}";
		}

		private static string RenderEnumAsJsonValue(ServiceEnumInfo enumInfo)
		{
			const int maxValues = 3;
			var values = enumInfo.Values.Where(x => !x.IsObsolete).ToList();
			return values.Count == 1 ? $"\"{values[0].Name}\"" :
				"\"(" + string.Join("|", values.Select(x => x.Name).Take(maxValues)) + (values.Count > maxValues ? "|..." : "") + ")\"";
		}

		private static readonly Dictionary<int, string> s_reasonPhrases = new Dictionary<int, string>
		{
			{ 100, "Continue" },
			{ 101, "Switching Protocols" },
			{ 200, "OK" },
			{ 201, "Created" },
			{ 202, "Accepted" },
			{ 203, "Non-Authoritative Information" },
			{ 204, "No Content" },
			{ 205, "Reset Content" },
			{ 206, "Partial Content" },
			{ 300, "Multiple Choices" },
			{ 301, "Moved Permanently" },
			{ 302, "Found" },
			{ 303, "See Other" },
			{ 304, "Not Modified" },
			{ 305, "Use Proxy" },
			{ 307, "Temporary Redirect" },
			{ 400, "Bad Request" },
			{ 401, "Unauthorized" },
			{ 402, "Payment Required" },
			{ 403, "Forbidden" },
			{ 404, "Not Found" },
			{ 405, "Method Not Allowed" },
			{ 406, "Not Acceptable" },
			{ 407, "Proxy Authentication Required" },
			{ 408, "Request Timeout" },
			{ 409, "Conflict" },
			{ 410, "Gone" },
			{ 411, "Length Required" },
			{ 412, "Precondition Failed" },
			{ 413, "Request Entity Too Large" },
			{ 414, "Request-Uri Too Long" },
			{ 415, "Unsupported Media Type" },
			{ 416, "Requested Range Not Satisfiable" },
			{ 417, "Expectation Failed" },
			{ 426, "Upgrade Required" },
			{ 500, "Internal Server Error" },
			{ 501, "Not Implemented" },
			{ 502, "Bad Gateway" },
			{ 503, "Service Unavailable" },
			{ 504, "Gateway Timeout" },
			{ 505, "Http Version Not Supported" },
		};

		private static readonly string[] s_pythonReserved = new string[]
		{
			"and",
			"as",
			"assert",
			"bool",
			"break",
			"class",
			"continue",
			"def",
			"del",
			"dict",
			"elif",
			"else",
			"except",
			"exec",
			"finally",
			"float",
			"for",
			"from",
			"global",
			"id",
			"if",
			"import",
			"in",
			"int",
			"is",
			"lambda",
			"list",
			"not",
			"or",
			"pass",
			"print",
			"raise",
			"return",
			"self",
			"set",
			"str",
			"try",
			"tuple",
			"while",
			"with",
			"yield",
		};
	}
}
