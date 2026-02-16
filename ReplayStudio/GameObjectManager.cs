using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace ReplayStudio
{
	internal class GameObjectManager
	{
		internal GameObject UiAssets;

		private void LoadAssetBundle()
		{
			UiAssets = GameObject.Instantiate(LoadAssetFromStream<GameObject>(Core.Instance, "UIFramework.Assets.uiframework", "UIFramework"));
			GameObject.DontDestroyOnLoad(UiAssets);
		}

		internal static T LoadAssetFromStream<T>(MelonMod instance, string path, string assetName) where T : UnityEngine.Object
		{
			using (System.IO.Stream bundleStream = instance.MelonAssembly.Assembly.GetManifestResourceStream(path))
			{
				Il2CppSystem.IO.Stream Il2CppStream = ConvertToIl2CppStream(bundleStream);
				AssetBundle bundle = AssetBundle.LoadFromStream(Il2CppStream);
				Il2CppStream.Close();
				T asset = bundle.LoadAsset<T>(assetName);
				bundle.Unload(false);
				return asset;
			}
		}

		internal static Il2CppSystem.IO.Stream ConvertToIl2CppStream(System.IO.Stream stream)
		{
			Il2CppSystem.IO.MemoryStream Il2CppStream = new Il2CppSystem.IO.MemoryStream();

			const int bufferSize = 4096;
			byte[] managedBuffer = new byte[bufferSize];
			Il2CppStructArray<byte> Il2CppBuffer = new(managedBuffer);

			int bytesRead;
			while ((bytesRead = stream.Read(managedBuffer, 0, managedBuffer.Length)) > 0)
			{
				Il2CppBuffer = managedBuffer;
				Il2CppStream.Write(Il2CppBuffer, 0, bytesRead);
			}
			Il2CppStream.Flush();
			return Il2CppStream;
		}

	}
}
