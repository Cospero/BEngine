﻿using ImGuiNET;
using Silk.NET.Input;

namespace BEngineEditor
{
	internal class MenuBarScreen : Screen
	{
		private ProjectContext _projectContext;

		private Project _project => _projectContext.CurrentProject;
		private ProjectCompiler _compiler => _projectContext.CurrentProject.Compiler;
		private ProjectSettings _settings => _projectContext.CurrentProject.Settings;

		protected override void Setup()
		{
			_projectContext = window.ProjectContext;
		}

		public override void Display()
		{
			ImGui.BeginMainMenuBar();

			if (_projectContext.CurrentProject == null)
				return;


			if (ImGui.BeginMenu("Actions"))
			{
				if (ImGui.MenuItem("Open Code Editor", "Ctrl+Shift+Q"))
				{
					Utils.OpenWithDefaultProgram(_projectContext.CurrentProject.SolutionPath);
				}

				if (_compiler.BuildingGame)
					return;

				if (ImGui.MenuItem("Load Project", "Ctrl+Shift+F"))
				{
					_projectContext.SearchingProject = true;
				}

				if (ImGui.MenuItem("Reload assembly", "Ctrl+Shift+B"))
				{
					_compiler.CompileScripts();
				}

				if (ImGui.MenuItem("Reload assembly", "Ctrl+Shift+B") && _project.OpenedScene != null)
				{
					_project.OpenedScene.Save<Scene>();
				}

				if (_compiler.AssemblyLoaded && _compiler.AssemblyCompileErrors.Count == 0)
				{
					if (ImGui.MenuItem("Build", "Ctrl+Shift+G"))
					{
						_compiler.BuildGame();
					}

					if (ImGui.MenuItem("Build and Run", "Ctrl+Shift+R"))
					{
						_compiler.BuildGame(true);
					}
				}

				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("BuildOS"))
			{
				if (ImGui.MenuItem("Windows", "", _compiler.IsCurrentOS(ProjectCompiler.Win64))) 
				{ 
					_settings.BuildOS = ProjectCompiler.Win64;
				}
				if (ImGui.MenuItem("Linux", "", _compiler.IsCurrentOS(ProjectCompiler.Linux64))) 
				{ 
					_settings.BuildOS = ProjectCompiler.Linux64;
				}
				if (ImGui.MenuItem("MacOS", "", _compiler.IsCurrentOS(ProjectCompiler.Osx64))) 
				{ 
					_settings.BuildOS = ProjectCompiler.Osx64;
				}

				ImGui.EndMenu();
			}

			ImGui.EndMainMenuBar();
		}
	}
}
