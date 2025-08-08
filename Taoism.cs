using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

using Terraria.ModLoader;

namespace Taoism
{
	
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class taoism : Mod
	{
		public static ModKeybind RiposteKey;
		public static ModKeybind EarthSpriteKey;

		public override void Load()
		{
			RiposteKey = KeybindLoader.RegisterKeybind(this, "5 Elements Riposte", "X");
			EarthSpriteKey = KeybindLoader.RegisterKeybind(this, "Earth Sprite World Evil", "NumPad5");
		}

		public override void Unload()
		{
			RiposteKey = null;
			EarthSpriteKey = null;
		}
	}
}



