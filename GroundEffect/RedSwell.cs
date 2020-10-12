using System;
using UnityEngine;

namespace KSP_GroundEffect
{
	[KSPAddon(KSPAddon.Startup.Instantly, false)]
	public class RedSwell : MonoBehaviour
	{
		public static bool FARDetected=false;
		public void Awake() {
			print ("[RedSwell]: Initiating RedSwell v6.9 Kerbal Spyware Lifetime Free Trial");
			print ("[RedSwell]: Probing Personal Information:");

			// ya i took this part from Bac9's procedural wings
			foreach (AssemblyLoader.LoadedAssembly personalInformation in AssemblyLoader.loadedAssemblies) {
				if (personalInformation.assembly.GetName ().Name.Equals ("FerramAerospaceResearch", StringComparison.InvariantCultureIgnoreCase)) {
					// FAR detected
					FARDetected = true;

					print ("[RedSwell]: Program received signal SIGSEGV, Segmentation Fault.");
                    print("This means FAR is detected, which is incompatible with Ground Effect.");
					return;

                    // too lazy to find out how to make this mod kill itself
                }
            }
			//if (!ModuleGroundEffect.ferramEnabled) {
			//	print ("[RedSwell]: Found: Discord Login Token");
		    //}
			print ("[RedSwell]: Uploading Personal Information...");
			print ("[RedSwell]: Sending Credit Card Credentials");
			print ("[RedSwell]: Transaction Complete");
		}
	}
}

