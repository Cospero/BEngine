﻿using ImGuiNET;
using System.Numerics;

namespace BEngineEditor
{
	internal class AssemblyStatusScreen : Screen
	{
		private ProjectContext _projectContext;

		private List<string> _compileErrors => _projectContext.CurrentProject.CompileErrors;

		protected override void Setup()
		{
			_projectContext = window.ProjectContext;
		}

		public override void Display()
		{
			ImGui.SetNextWindowSize(new Vector2(ImGui.GetWindowSize().X, ImGui.GetWindowSize().Y / 5), ImGuiCond.FirstUseEver);

			ImGui.Begin("Assembly Status", ImGuiWindowFlags.AlwaysAutoResize);

			if (_projectContext.CurrentProject.AssemblyLoaded == false)
			{
				ImGui.Text($"Building assembly... (Build for {Math.Round((DateTime.Now - 
					_projectContext.CurrentProject.AssemblyBuildStartTime).TotalSeconds, 1)} sec)");
				return;
			}

			if (_compileErrors.Count == 0)
			{
				ImGui.Text($"Working clear, no errors found! (Build in " +
					$"{_projectContext.CurrentProject.AssemblyBuildEndTime.ToString("HH:mm:ss")}, " +
					$"{Math.Round((_projectContext.CurrentProject.AssemblyBuildEndTime -
					_projectContext.CurrentProject.AssemblyBuildStartTime).TotalSeconds, 1)} sec)");
				return;
			}

			for (int i = 0; i < _compileErrors.Count; i++)
			{
				Vector4 red = new Vector4(1, 0, 0, 1);
				string data = _compileErrors[i];
				ImGui.PushID(i);
				ImGui.PushItemWidth(ImGui.GetWindowSize().X);
				ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
				ImGui.PushStyleColor(ImGuiCol.Text, red);
				ImGui.InputText(string.Empty, ref data, 1024, ImGuiInputTextFlags.ReadOnly);
				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
				ImGui.PopItemWidth();
				ImGui.PopID();
			}

			ImGui.End();
		}
	}
}
