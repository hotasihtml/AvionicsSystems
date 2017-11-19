﻿/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2017 MOARdV
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 * 
 ****************************************************************************/
using MoonSharp.Interpreter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// MASLoader loads data at startup.
    /// 
    /// It is also the generic bucket for global data, since it's loading the
    /// fonts, colors, scripts, and everything else anyway.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class MASLoader : MonoBehaviour
    {
        /// <summary>
        /// Version of the DLL.
        /// </summary>
        static public string masVersion;

        /// <summary>
        /// Fonts that have been loaded (AssetBundle fonts, user bitmap fonts,
        /// or system fonts).
        /// </summary>
        static public Dictionary<string, List<Font>> fonts = new Dictionary<string, List<Font>>();

        /// <summary>
        /// List of all radio navigation beacons found in the installation.
        /// </summary>
        static public List<NavAid> navaids = new List<NavAid>();

        /// <summary>
        /// List of the known system fonts.
        /// </summary>
        static private string[] systemFonts;

        /// <summary>
        /// Dictionary of all shaders found in the asset bundle.
        /// </summary>
        static public Dictionary<string, Shader> shaders = new Dictionary<string, Shader>();

        /// <summary>
        /// Text of all of the scripts found in config nodes.
        /// </summary>
        static public List<string> userScripts = new List<string>();

        /// <summary>
        /// Dictionary of all RPM-compatible named colors.
        /// </summary>
        static public Dictionary<string, Color32> namedColors = new Dictionary<string, Color32>();

        static public HashSet<string> knownAssemblies = new HashSet<string>();

        MASLoader()
        {
            DontDestroyOnLoad(this);
        }

        /// <summary>
        /// Load a named font - preferably using an AssetBundle font, but also
        /// allowing system fonts.
        /// </summary>
        /// <param name="fontName">The name of the font to load.</param>
        /// <param name="texture">[out] Texture to return</param>
        /// <returns>The font</returns>
        internal static Font GetFont(string fontName)
        {
            if (fonts.ContainsKey(fontName))
            {
                return fonts[fontName][0];
            }
            else if (systemFonts == null)
            {
                systemFonts = Font.GetOSInstalledFontNames();
            }

            string toFind = Array.Find(systemFonts, (string s) => { return (s == fontName); });
            if (string.IsNullOrEmpty(toFind))
            {
                // If the font isn't recognized as a system font, fall back to
                // Liberation Sans.
                Utility.LogErrorMessage("Need to update a font name: {1} (ignore {0})", 0, fontName);
                if (fonts.ContainsKey("LiberationSans-Regular"))
                {
                    return fonts["Liberation Sans"][0];
                }
                else
                {
                    throw new ArgumentException("Unable to find font " + fontName);
                }
            }
            else
            {
                Font dynamicFont = Font.CreateDynamicFontFromOSFont(fontName, 32);
                List<Font> fontList = new List<Font>();
                fontList.Add(dynamicFont);
                fonts[fontName] = fontList;

                return dynamicFont;
            }
        }

        /// <summary>
        /// Awake() - Load components used by the mod.
        /// </summary>
        public void Awake()
        {
            masVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
            UnityEngine.Debug.Log(String.Format("[MASLoader] MOARdV's Avionics Systems version {0}", masVersion));

            if (!GameDatabase.Instance.IsReady())
            {
                Utility.LogErrorMessage(this, "GameDatabase.IsReady is false");
                throw new Exception("MASLoader: GameDatabase is not ready.  Unable to continue.");
            }

            LoadAssets();

            StartCoroutine("LoadAvionicsSystemAssets");
            RegisterWithModuleManager();

            for (int i = AssemblyLoader.loadedAssemblies.Count - 1; i >= 0; --i)
            {
                string assemblyName = AssemblyLoader.loadedAssemblies[i].assembly.GetName().Name;
                knownAssemblies.Add(assemblyName);
            }
        }

        /// <summary>
        /// Coroutine for adding scripts to the Lua context.  Paced to load one
        /// string per frame.
        /// 
        /// It also looks for existing global RasterPropMonitor COLOR_ definitions.
        /// </summary>
        /// <returns>null when done</returns>
        private IEnumerator LoadAvionicsSystemAssets()
        {
            userScripts.Clear();
            ConfigNode[] userScriptNodes = GameDatabase.Instance.GetConfigNodes("MAS_LUA");
            if (userScriptNodes.Length > 0)
            {
                for (int nodeIdx = 0; nodeIdx < userScriptNodes.Length; ++nodeIdx)
                {
                    if (userScriptNodes[nodeIdx].HasValue("name"))
                    {
                        ConfigNode node = userScriptNodes[nodeIdx];
                        string[] scripts = node.GetValues("script");
                        Utility.LogMessage(this, "Parsing MAS_LUA node \"{0}\" ({1} script references)", node.GetValue("name"), scripts.Length);

                        for (int scriptIdx = 0; scriptIdx < scripts.Length; ++scriptIdx)
                        {
                            userScripts.Add(string.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + scripts[scriptIdx], Encoding.UTF8)));
                            yield return new WaitForEndOfFrame();
                        }
                    }
                }
            }

            namedColors.Clear();
            ConfigNode[] rpmColorNodes = GameDatabase.Instance.GetConfigNodes("RPM_GLOBALCOLORSETUP");
            for (int colorNodeIdx = 0; colorNodeIdx < rpmColorNodes.Length; ++colorNodeIdx)
            {
                ConfigNode[] colorDef = rpmColorNodes[colorNodeIdx].GetNodes("COLORDEFINITION");
                for (int defIdx = 0; defIdx < colorDef.Length; ++defIdx)
                {
                    if (colorDef[defIdx].HasValue("name") && colorDef[defIdx].HasValue("color"))
                    {
                        string name = "COLOR_" + (colorDef[defIdx].GetValue("name").Trim());
                        Color32 color = ConfigNode.ParseColor32(colorDef[defIdx].GetValue("color").Trim());
                        if (namedColors.ContainsKey(name))
                        {
                            namedColors[name] = color;
                        }
                        else
                        {
                            namedColors.Add(name, color);
                        }

                        //Utility.LogMessage(this, "{0} = {1}", name, color);
                    }
                }
                yield return new WaitForEndOfFrame();
            }
            yield return null;
        }

        /// <summary>
        /// Tries to load a font based on a config-reference bitmap.
        /// </summary>
        /// <param name="node"></param>
        private void LoadBitmapFont(ConfigNode node)
        {
            // TODO: Meaningful error messages

            // All nodes are required
            string name = string.Empty;
            if (!node.TryGetValue("name", ref name))
            {
                Utility.LogErrorMessage(this, "No name in bitmap font");
                return;
            }

            string texName = string.Empty;
            if (!node.TryGetValue("texture", ref texName))
            {
                Utility.LogErrorMessage(this, "No texture in bitmap font");
                return;
            }

            string fontDefinitionName = string.Empty;
            if (!node.TryGetValue("fontDefinition", ref fontDefinitionName))
            {
                Utility.LogErrorMessage(this, "No fontDefinition in bitmap font");
                return;
            }

            Vector2 fontSize = Vector2.zero;
            if (!node.TryGetValue("fontSize", ref fontSize))
            {
                Utility.LogErrorMessage(this, "No fontSize in bitmap font");
                return;
            }
            if (fontSize.x <= 0 || fontSize.y <= 0)
            {
                Utility.LogErrorMessage(this, "invalid font sizein bitmap font");
                return;
            }

            Texture2D fontTex = GameDatabase.Instance.GetTexture(texName, false);
            if (fontTex == null)
            {
                // Font doesn't exist
                Utility.LogErrorMessage(this, "Can't load texture {0}", texName);
                return;
            }

            string fontDefinition = File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + fontDefinitionName, Encoding.UTF8)[0];
            if (string.IsNullOrEmpty(fontDefinition))
            {
                Utility.LogErrorMessage(this, "Can't open font definition file {0}", fontDefinitionName);
                return;
            }

            // We now know everything we need to create a Font
            Font newFont = new Font(name);
            newFont.material = new Material(shaders["MOARdV/TextMesh"]);
            newFont.material.mainTexture = fontTex;

            int charWidth = (int)fontSize.x;
            int charHeight = (int)fontSize.y;
            int numCharacters = fontDefinition.Length;
            CharacterInfo[] charInfo = new CharacterInfo[numCharacters];
            int charsPerRow = fontTex.width / charWidth;
            int rowsInImage = fontTex.height / charHeight;
            Vector2 uv = new Vector2(fontSize.x / (float)fontTex.width, fontSize.y / (float)fontTex.height);
            int charIndex = 0;
            for (int row = 0; row < rowsInImage; ++row)
            {
                for (int column = 0; column < charsPerRow; ++column)
                {
                    charInfo[charIndex].advance = charWidth;
                    charInfo[charIndex].bearing = 0;
                    charInfo[charIndex].glyphHeight = charHeight;
                    charInfo[charIndex].glyphWidth = charWidth;
                    charInfo[charIndex].index = fontDefinition[charIndex];
                    charInfo[charIndex].maxX = charWidth;
                    charInfo[charIndex].maxY = charHeight;
                    charInfo[charIndex].minX = 0;
                    charInfo[charIndex].minY = 0;
                    charInfo[charIndex].size = 0;
                    charInfo[charIndex].style = FontStyle.Normal;
                    charInfo[charIndex].uvBottomLeft = new Vector2(column * uv.x, (rowsInImage - row - 1) * uv.y);
                    charInfo[charIndex].uvBottomRight = new Vector2((column + 1) * uv.x, (rowsInImage - row - 1) * uv.y);
                    charInfo[charIndex].uvTopLeft = new Vector2(column * uv.x, (rowsInImage - row) * uv.y);
                    charInfo[charIndex].uvTopRight = new Vector2((column + 1) * uv.x, (rowsInImage - row) * uv.y);
                    ++charIndex;
                    if (charIndex >= numCharacters)
                    {
                        break;
                    }
                }
            }

            newFont.characterInfo = charInfo;

            Utility.LogMessage(this, "Adding bitmap font {0} with {1} characters", name, numCharacters);

            List<Font> fontList = new List<Font>();
            fontList.Add(newFont);
            fonts[name] = fontList;
        }

        /// <summary>
        /// Locate the requested asset bundle, load it, and return it.
        /// </summary>
        /// <param name="formatString">The format string to apply to the suffix.</param>
        /// <param name="suffix">The suffix to apply to the formatString.</param>
        /// <returns>null on error, otherwise, the asset bundle.</returns>
        private AssetBundle LoadAssetBundle(string formatString, string suffix)
        {
            string assetBundleName = string.Format(formatString, suffix);
            WWW www = new WWW(assetBundleName);

            if (!string.IsNullOrEmpty(www.error))
            {
                Utility.LogErrorMessage(this, "Error loading AssetBundle {1}: {0}", www.error, assetBundleName);
                return null;
            }
            else if (www.assetBundle == null)
            {
                Utility.LogErrorMessage(this, "Unable to load AssetBundle {0}", assetBundleName);
                return null;
            }

            return www.assetBundle;
        }

        /// <summary>
        /// Load out assets through the asset bundle system.
        /// </summary>
        private void LoadAssets()
        {
            StringBuilder sb = Utility.GetStringBuilder();
            sb.Append("file://").Append(KSPUtil.ApplicationRootPath).Append("GameData/MOARdV/AvionicsSystems/mas-{0}.assetbundle");
            string assetFormat = sb.ToString();

            string platform = string.Empty;
            switch (Application.platform)
            {
                case RuntimePlatform.LinuxPlayer:
                    platform = "linux";
                    break;
                case RuntimePlatform.OSXPlayer:
                    platform = "osx";
                    break;
                case RuntimePlatform.WindowsPlayer:
                    platform = "windows";
                    break;
                default:
                    Utility.LogErrorMessage(this, "Unsupported/unexpected platform {0}", Application.platform);
                    return;
            }

            shaders.Clear();
            AssetBundle bundle = LoadAssetBundle(assetFormat, platform);
            if (bundle == null)
            {
                return;
            }

            string[] assetNames = bundle.GetAllAssetNames();
            int len = assetNames.Length;

            Shader shader;
            for (int i = 0; i < len; i++)
            {
                if (assetNames[i].EndsWith(".shader"))
                {
                    shader = bundle.LoadAsset<Shader>(assetNames[i]);
                    if (!shader.isSupported)
                    {
                        Utility.LogErrorMessage(this, "Shader {0} - unsupported in this configuration", shader.name);
                    }
                    shaders[shader.name] = shader;
                }
            }

            bundle.Unload(false);

            fonts.Clear();
            bundle = LoadAssetBundle(assetFormat, "font");
            if (bundle == null)
            {
                return;
            }

            assetNames = bundle.GetAllAssetNames();
            len = assetNames.Length;

            Font font;
            for (int i = 0; i < len; i++)
            {
                if (assetNames[i].EndsWith(".ttf"))
                {
                    font = bundle.LoadAsset<Font>(assetNames[i]);

                    string[] fnames = font.fontNames;
                    if (fnames.Length == 0)
                    {
                        Utility.LogErrorMessage(this, "Font {0} - did not find fontName.", font.name);
                    }
                    else
                    {
                        if (fonts.ContainsKey(fnames[0]))
                        {
                            // TODO: Do I need to keep all of the fonts in this dictionary?  Or is one
                            // adequate?
                            fonts[fnames[0]].Add(font);
                        }
                        else
                        {
                            Utility.LogMessage(this, "Adding font \"{0}\" from asset bundle.", fnames[0]);
                            List<Font> fontList = new List<Font>();
                            fontList.Add(font);
                            fonts[fnames[0]] = fontList;
                        }
                    }
                }
            }
            bundle.Unload(false);

            Utility.LogInfo(this, "Found {0} RPM shaders and {1} fonts.", shaders.Count, fonts.Count);

            // User fonts.  We put them here to make sure that internal
            // shaders exist already.
            ConfigNode[] masBitmapFont = GameDatabase.Instance.GetConfigNodes("MAS_BITMAP_FONT");
            for (int masFontIdx = 0; masFontIdx < masBitmapFont.Length; ++masFontIdx)
            {
                LoadBitmapFont(masBitmapFont[masFontIdx]);
            }

            // Generate our list of radio navigation beacons.
            navaids.Clear();
            ConfigNode[] navaidGroupNode = GameDatabase.Instance.GetConfigNodes("MAS_NAVAID");
            for (int navaidGroupIdx = 0; navaidGroupIdx  < navaidGroupNode.Length; ++navaidGroupIdx )
            {
                ConfigNode[] navaidNode = navaidGroupNode[navaidGroupIdx].GetNodes("NAVAID");
                for (int navaidIdx = 0; navaidIdx < navaidNode.Length; ++navaidIdx)
                {
                    bool canAdd = true;
                    NavAid navaid = new NavAid();
                    navaid.distanceToHorizon = -1.0;
                    navaid.distanceToHorizonDME = -1.0;

                    navaid.name = string.Empty;
                    if (!navaidNode[navaidIdx].TryGetValue("name", ref navaid.name))
                    {
                        Utility.LogErrorMessage(this, "Did not get 'name' for NAVAID");
                        canAdd = false;
                    }

                    navaid.identifier = string.Empty;
                    if (!navaidNode[navaidIdx].TryGetValue("id", ref navaid.identifier))
                    {
                        Utility.LogErrorMessage(this, "Did not get 'id' for NAVAID {0}", navaid.name);
                        canAdd = false;
                    }

                    navaid.celestialName = string.Empty;
                    if (!navaidNode[navaidIdx].TryGetValue("celestialName", ref navaid.celestialName))
                    {
                        Utility.LogErrorMessage(this, "Did not get 'celestialName' for NAVAID {0}", navaid.name);
                        canAdd = false;
                    }

                    navaid.frequency = 0.0f;
                    if (!navaidNode[navaidIdx].TryGetValue("frequency", ref navaid.frequency))
                    {
                        Utility.LogErrorMessage(this, "Did not get 'frequency' for NAVAID {0}", navaid.name);
                        canAdd = false;
                    }

                    navaid.latitude = 0.0;
                    if (!navaidNode[navaidIdx].TryGetValue("latitude", ref navaid.latitude))
                    {
                        Utility.LogErrorMessage(this, "Did not get 'latitude' for NAVAID {0}", navaid.name);
                        canAdd = false;
                    }

                    navaid.longitude = 0.0;
                    if (!navaidNode[navaidIdx].TryGetValue("longitude", ref navaid.longitude))
                    {
                        Utility.LogErrorMessage(this, "Did not get 'longitude' for NAVAID {0}", navaid.name);
                        canAdd = false;
                    }

                    navaid.altitude = 0.0;
                    if (!navaidNode[navaidIdx].TryGetValue("altitude", ref navaid.altitude))
                    {
                        Utility.LogErrorMessage(this, "Did not get 'altitude' for NAVAID {0}", navaid.name);
                        canAdd = false;
                    }

                    string type = string.Empty;
                    if (!navaidNode[navaidIdx].TryGetValue("type", ref type))
                    {
                        Utility.LogErrorMessage(this, "Did not get 'type' for NAVAID {0}", navaid.name);
                        canAdd = false;
                    }
                    switch (type)
                    {
                        case "NDB":
                            navaid.type = NavAidType.NDB;
                            break;
                        case "NDB DME":
                            navaid.type = NavAidType.NDB_DME;
                            break;
                        case "VOR":
                            navaid.type = NavAidType.VOR;
                            break;
                        case "VOR DME":
                            navaid.type = NavAidType.VOR_DME;
                            break;
                        case "ILS":
                            navaid.type = NavAidType.ILS;
                            break;
                        case "ILS DME":
                            navaid.type = NavAidType.ILS_DME;
                            break;
                        default:
                            Utility.LogErrorMessage(this, "Did not get valid 'type' for NAVAID {0}", navaid.name);
                            canAdd = false;
                            break;
                    }

                    if (canAdd)
                    {
                        navaids.Add(navaid);
                    }
                }
            }
        }

        /// <summary>
        /// Trigger a coroutine that will reload values that MM may have changed.
        /// </summary>
        public void PostPatchCallback()
        {
            StartCoroutine("LoadAvionicsSystemAssets");
        }

        /// <summary>
        /// Let ModuleManager know that I care about it reloading and patching values.
        /// </summary>
        private void RegisterWithModuleManager()
        {
            Type mmPatchLoader = null;
            AssemblyLoader.loadedAssemblies.TypeOperation(t =>
            {
                if (t.FullName == "ModuleManager.MMPatchLoader")
                {
                    mmPatchLoader = t;
                }
            });

            if (mmPatchLoader == null)
            {
                return;
            }

            MethodInfo addPostPatchCallback = mmPatchLoader.GetMethod("addPostPatchCallback", BindingFlags.Static | BindingFlags.Public);

            if (addPostPatchCallback == null)
            {
                return;
            }

            try
            {
                var parms = addPostPatchCallback.GetParameters();
                if (parms.Length < 1)
                {
                    return;
                }

                Delegate callback = Delegate.CreateDelegate(parms[0].ParameterType, this, "PostPatchCallback");

                object[] args = new object[] { callback };

                addPostPatchCallback.Invoke(null, args);
            }
            catch (Exception e)
            {
                Utility.LogMessage(this, "addPostPatchCallback threw {0}", e);
            }
        }
    }
}
