using MonoMod.ModInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Randomizer.Interoperability {

	/// <summary>
	/// Interop class to allow other mods to trigger randomizer generation
	/// </summary>
	[ModExportName("Randomizer.GenerationInterop")]
	public static class GenerationInterop {
		private static Builder.BuilderStatus status = Builder.BuilderStatus.NotStarted;
		private static AreaKey? generatedArea = null;

		/// <summary>
		/// Starts randomizer map generation
		/// </summary>
		/// <param name="settings">the settings object to use for generation</param>
		/// <returns>True if generation was successfully started, false if it could not be started</returns>
		/// <exception cref="ArgumentException">thrown when the settings parameter is not an instance of RandoSettings</exception>
		public static bool Generate(object settings) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			if (RandoModule.MapBuilder != null) {
				Logger.Log(LogLevel.Warn, "Randomizer.GenerationInterop", "Cannot start generation; another is already in progress");
				return false;
			}
			Builder b = new Builder();
			b.OnAbort = () => {
				status = Builder.BuilderStatus.Abort;
			};
			b.OnError = (Exception e) => {
				status = Builder.BuilderStatus.Error;
			};
			b.OnSuccess = (AreaKey newArea) => {
				generatedArea = newArea;
				status = Builder.BuilderStatus.Success;
			};
			RandoModule.MapBuilder = b;
			b.Go(s);
			status = Builder.BuilderStatus.Running;
			generatedArea = null;
			return true;
		}

		/// <summary>
		/// Gets whether rando map generation via interop is currently running
		/// </summary>
		/// <returns>true if an interop-initiated map generation is in progress, otherwise false</returns>
		public static bool GenerationInProgress() {
			return status == Builder.BuilderStatus.Running;
		}

		/// <summary>
		/// Gets whether rando map generation via interop is finished and the map ready to enter
		/// </summary>
		/// <returns>true if an interop-initiated map generation is finished and the area ready, otherwise false</returns>
		public static bool ReadyToLaunch() {
			return status == Builder.BuilderStatus.Success && generatedArea != null;
		}

		public static AreaKey? GetGeneratedArea() {
			return generatedArea;
		}

		/// <summary>
		/// Enter the rando level generated via interop
		/// </summary>
		/// <returns>true if the launch provccess was started successfully, otherwise false</returns>
		public static bool EnterGeneratedArea() {
			if (!ReadyToLaunch()) return false;
			RandoModule.LaunchIntoRandoArea(generatedArea.Value);
			status = Builder.BuilderStatus.NotStarted;
			generatedArea = null;
			return true;
		}

	}
}
