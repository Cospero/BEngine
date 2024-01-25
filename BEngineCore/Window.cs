﻿using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;

namespace BEngineCore
{
	public class Window
	{
		protected GameWindow _window;

		public Window()
		{
			_window = new GameWindow(GameWindowSettings.Default, NativeWindowSettings.Default);

			_window.Load += OnLoad;
			_window.Resize += OnResize;
			_window.RenderFrame += OnRenderFrame;
		}

		protected virtual void OnResize() { }
		protected virtual void OnLoad() { }
		protected virtual void OnPreRender(float time) { }
		protected virtual void OnRender() { }
		protected virtual void OnPostRender() { }

		private void OnResize(OpenTK.Windowing.Common.ResizeEventArgs obj)
		{
			GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y);

			OnResize();
		}

		private void OnRenderFrame(OpenTK.Windowing.Common.FrameEventArgs obj)
		{
			OnPreRender((float)obj.Time);

			GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

			OnRender();

			OnPostRender();

			_window.SwapBuffers();
		}

		public void Run()
		{
			_window.Run();
		}
	}
}
