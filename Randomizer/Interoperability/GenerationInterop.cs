using MonoMod.ModInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Randomizer.Interoperability {
	[ModExportName("Randomizer.GenerationInterop")]
	public static class GenerationInterop {
		public static object Generate(object settings) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			// TODO (corkr900)
			return null;
		}
	}
}
