﻿using BEngineCore;

namespace BEngineEditor
{
	public class Project
	{
		private Scripting _scripting;
		private AssetWorker _assets;
		private ProjectCompiler _compiler;
		private AssemblyListener _assemblyListener;
		private Logger _logger = new Logger();

		public string Name { get; private set; } = string.Empty;
		public string Directory { get; private set; } = string.Empty;
		public ProjectSettings Settings { get; private set; }

		public AssemblyListener AssemblyListener => _assemblyListener;
		public ProjectCompiler Compiler => _compiler;
		public Logger Logger => _logger;

		public string SolutionPath => $@"{Directory}\{Name}.sln";
		public string ProjectAssemblyDirectory => $@"{Directory}\{Name}Assembly";
		public string ProjectBuildDirectory => $@"{Directory}\{Name}Build";
		public string ProjectBuildBinaryDirectory => $@"{ProjectBuildDirectory}\bin\Release\net8.0\";
		public string ProjectAssemblyPath => $@"{ProjectAssemblyDirectory}\{Name}Assembly.csproj";
		public string AssemblyBinaryPath =>  $@"{ProjectAssemblyDirectory}\bin\Debug\net8.0\{Name}Assembly.dll";
		public string AssetsDirectory => $@"{Directory}\Assets";
		public bool EditorAssemblyExists => File.Exists(AssemblyBinaryPath);

		public Project(string name, string directory)
		{
			Name = name;
			Directory = directory;
			Settings = new(this);

			ProjectSettings? settings = Settings.Load();
			if (settings != null)
				Settings = settings;

			Settings.UpdateResultPath(this);
		}

		public void LoadProjectData()
		{
			_compiler = new ProjectCompiler(this);
			_compiler.CompileScripts();

			_assemblyListener = new();
			_assemblyListener.InitializeScriptWatch(this);
			_assemblyListener.OnScriptsChanged += (e) => _compiler.CompileScripts();

			_assets = new AssetWorker(this);

			// Get files and etc.
		}
	}
}