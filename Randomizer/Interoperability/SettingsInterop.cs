using MonoMod.ModInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Randomizer.Interoperability {

	/// <summary>
	/// Interop class that allows other mods to build a RandoSettings object
	/// </summary>
	[ModExportName("Randomizer.SettingsInterop")]
	public static class SettingsInterop {

		/// <summary>
		/// Creates an instance of RandoSettings
		/// </summary>
		/// <returns></returns>
		public static object GetSettingsObject() {
			return new RandoSettings();
		}

		/// <summary>
		/// Enables the given map in the given settings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="area">The map to enable</param>
		/// <exception cref="ArgumentException">Thrown when the parameter is not an instance of RandoSettings</exception>
		public static void EnableMap(object settings, AreaKey area) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			s.EnableMap(area);
		}

		/// <summary>
		/// Enables all given maps in the given settings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="areas">An enumerable set of maps to enable</param>
		/// <exception cref="ArgumentException">Thrown when the parameter is not an instance of RandoSettings</exception>
		public static void EnableMaps(object settings, IEnumerable<AreaKey> areas) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			if (areas == null) throw new ArgumentNullException(nameof(areas));
            foreach (var area in areas)
            {
				s.EnableMap(area);
            }
		}

		/// <summary>
		/// Enables all vanilla maps in the given settings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <exception cref="ArgumentException">Thrown when the parameter is not an instance of RandoSettings</exception>
		public static void EnableVanillaMaps(object settings) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);

			s.EnableMap(new AreaKey(0, AreaMode.Normal));
			s.EnableMap(new AreaKey(1, AreaMode.Normal));
			s.EnableMap(new AreaKey(2, AreaMode.Normal));
			s.EnableMap(new AreaKey(3, AreaMode.Normal));
			s.EnableMap(new AreaKey(4, AreaMode.Normal));
			s.EnableMap(new AreaKey(5, AreaMode.Normal));
			s.EnableMap(new AreaKey(6, AreaMode.Normal));
			s.EnableMap(new AreaKey(7, AreaMode.Normal));
			s.EnableMap(new AreaKey(9, AreaMode.Normal));
			s.EnableMap(new AreaKey(10, AreaMode.Normal));

			s.EnableMap(new AreaKey(1, AreaMode.BSide));
			s.EnableMap(new AreaKey(2, AreaMode.BSide));
			s.EnableMap(new AreaKey(3, AreaMode.BSide));
			s.EnableMap(new AreaKey(4, AreaMode.BSide));
			s.EnableMap(new AreaKey(5, AreaMode.BSide));
			s.EnableMap(new AreaKey(6, AreaMode.BSide));
			s.EnableMap(new AreaKey(7, AreaMode.BSide));
			s.EnableMap(new AreaKey(9, AreaMode.BSide));

			s.EnableMap(new AreaKey(1, AreaMode.CSide));
			s.EnableMap(new AreaKey(2, AreaMode.CSide));
			s.EnableMap(new AreaKey(3, AreaMode.CSide));
			s.EnableMap(new AreaKey(4, AreaMode.CSide));
			s.EnableMap(new AreaKey(5, AreaMode.CSide));
			s.EnableMap(new AreaKey(6, AreaMode.CSide));
			s.EnableMap(new AreaKey(7, AreaMode.CSide));
			s.EnableMap(new AreaKey(9, AreaMode.CSide));
		}

		/// <summary>
		/// Sets the Rules value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set</param>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static void SetSeed(object settings, string seed) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			s.Seed = seed;
		}

		/// <summary>
		/// Sets the Rules value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set</param>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static void SetRules(object settings, string rules) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			s.Rules = rules;
		}

		/// <summary>
		/// Sets the SeedType value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set. Will be parsed to the appropriate enum.</param>
		/// <returns>True if the arguments were valid and the value was set, false if the value is not a valid enum value</returns>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static bool SetSeedType(object settings, string seedTypeStr) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			return Enum.TryParse(seedTypeStr, out s.SeedType);
		}

		/// <summary>
		/// Sets the Algorithm value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set. Will be parsed to the appropriate enum.</param>
		/// <returns>True if the arguments were valid and the value was set, false if the value is not a valid enum value</returns>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static bool SetAlgorithm(object settings, string algorithmStr) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			return Enum.TryParse(algorithmStr, out s.Algorithm);
		}

		/// <summary>
		/// Sets the Dashes value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set. Will be parsed to the appropriate enum.</param>
		/// <returns>True if the arguments were valid and the value was set, false if the value is not a valid enum value</returns>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static bool SetDashes(object settings, string dashesStr) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			return Enum.TryParse(dashesStr, out s.Dashes);
		}

		/// <summary>
		/// Sets the Difficulty value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set. Will be parsed to the appropriate enum.</param>
		/// <returns>True if the arguments were valid and the value was set, false if the value is not a valid enum value</returns>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static bool SetDifficulty(object settings, string difficultyStr) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			return Enum.TryParse(difficultyStr, out s.Difficulty);
		}

		/// <summary>
		/// Sets the Difficulty Eagerness value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set. Will be parsed to the appropriate enum.</param>
		/// <returns>True if the arguments were valid and the value was set, false if the value is not a valid enum value</returns>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static bool SetDifficultyEagerness(object settings, string difficultyEagernessStr) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			return Enum.TryParse(difficultyEagernessStr, out s.DifficultyEagerness);
		}

		/// <summary>
		/// Sets the Length value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set. Will be parsed to the appropriate enum.</param>
		/// <returns>True if the arguments were valid and the value was set, false if the value is not a valid enum value</returns>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static bool SetLength(object settings, string lengthStr) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			return Enum.TryParse(lengthStr, out s.Length);
		}

		/// <summary>
		/// Sets the Lights value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set. Will be parsed to the appropriate enum.</param>
		/// <returns>True if the arguments were valid and the value was set, false if the value is not a valid enum value</returns>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static bool SetLights(object settings, string lightsStr) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			return Enum.TryParse(lightsStr, out s.Lights);
		}

		/// <summary>
		/// Sets the Darkness value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set. Will be parsed to the appropriate enum.</param>
		/// <returns>True if the arguments were valid and the value was set, false if the value is not a valid enum value</returns>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static bool SetDarkness(object settings, string darknessStr) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			return Enum.TryParse(darknessStr, out s.Darkness);
		}

		/// <summary>
		/// Sets the Strawberries value on the given RandoSettings object
		/// </summary>
		/// <param name="settings">The settings object</param>
		/// <param name="strawberriesStr">The value to set. Will be parsed to the appropriate enum.</param>
		/// <returns>True if the arguments were valid and the value was set, false if the value is not a valid enum value</returns>
		/// <exception cref="ArgumentException">Thrown when the first parameter is not an instance of RandoSettings</exception>
		public static bool SetStrawberries(object settings, string strawberriesStr) {
			RandoSettings s = settings as RandoSettings ?? InteropHelper.TypeError<RandoSettings>(settings);
			return Enum.TryParse(strawberriesStr, out s.Strawberries);
		}

	}
}
