using System;
using UnityEngine;

namespace KSP_GroundEffect
{
	[KSPAddon(KSPAddon.Startup.Instantly, false)]
	public class MagentaCarapace : MonoBehaviour
	{
		public void Awake() {
			print ("[MagentaCarapace]: Initiating MagentaCarapace v0.1 Kerbal Spyware Lifetime Free Trial");
			print ("[MagentaCarapace]: Probing Personal Information:");

			// ya i took this part from Bac9's procedural wings
			foreach (AssemblyLoader.LoadedAssembly personalInformation in AssemblyLoader.loadedAssemblies) {
				if (personalInformation.assembly.GetName ().Name.Equals ("FerramAerospaceResearch", StringComparison.InvariantCultureIgnoreCase)) {
					// FAR detected
					print ("[MagentaCarapace]: Found: Inappropriate Imagery");
					ModuleGroundEffect.ferramEnabled = true;
				}
			}
			if (!ModuleGroundEffect.ferramEnabled) {
				print ("[MagentaCarapace]: Found: Discord Login Token");
			}
			print ("[MagentaCarapace]: Uploading Personal Information...");
			print ("[MagentaCarapace]: Sending Credit Card Credentials");
			print ("[MagentaCarapace]: Transaction Complete");
		}
	}
}

