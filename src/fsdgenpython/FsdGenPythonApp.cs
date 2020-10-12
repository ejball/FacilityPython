using System;
using System.Collections.Generic;
using ArgsReading;
using Facility.CodeGen.Console;
using Facility.CodeGen.Python;
using Facility.Definition.CodeGen;

namespace fsdgenpython
{
	public sealed class FsdGenPythonApp : CodeGeneratorApp
	{
		public static int Main(string[] args)
		{
			return new FsdGenPythonApp().Run(args);
		}

		protected override IReadOnlyList<string> Description => new[]
		{
			"Generates Markdown for a Facility Service Definition.",
		};

		protected override IReadOnlyList<string> ExtraUsage => Array.Empty<string>();

		protected override CodeGenerator CreateGenerator() => new PythonGenerator();

		protected override FileGeneratorSettings CreateSettings(ArgsReader args) =>
			new PythonGeneratorSettings { };
	}
}
