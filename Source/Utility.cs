﻿/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 MOARdV
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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvionicsSystems
{
    internal static class Utility
    {
        internal static readonly string[] NewLine = { Environment.NewLine };

        #region Message Logging
        /// <summary>
        /// Log a message
        /// </summary>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogMessage(string format, params object[] values)
        {
            UnityEngine.Debug.Log(String.Format("[AvionicsSystems] " + format, values));
        }

        /// <summary>
        /// Log a message associated with an object.
        /// </summary>
        /// <param name="who"></param>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogMessage(object who, string format, params object[] values)
        {
            UnityEngine.Debug.Log(String.Format("[" + who.GetType().Name + "] " + format, values));
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogErrorMessage(string format, params object[] values)
        {
            UnityEngine.Debug.LogError(String.Format("[AvionicsSystems] " + format, values));
        }

        /// <summary>
        /// Log an error associated with an object
        /// </summary>
        /// <param name="who"></param>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogErrorMessage(object who, string format, params object[] values)
        {
            UnityEngine.Debug.LogError(String.Format("[" + who.GetType().Name + "] " + format, values));
        }
        #endregion

        /// <summary>
        /// Returns true if the value falls between the two extents (order independent)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="extent1"></param>
        /// <param name="extent2"></param>
        /// <returns></returns>
        internal static bool Between(this double value, double extent1, double extent2)
        {
            if (extent1 < extent2)
            {
                return (value >= extent1 && value <= extent2);
            }
            else
            {
                return (value <= extent1 && value >= extent2);
            }
        }

        /// <summary>
        /// Clamp a double between two extents
        /// </summary>
        /// <param name="value"></param>
        /// <param name="extent1"></param>
        /// <param name="extent2"></param>
        /// <returns></returns>
        internal static double Clamp(this double value, double extent1, double extent2)
        {
            if (extent1 < extent2)
            {
                value = Math.Max(Math.Min(value, extent2), extent1);
            }
            else
            {
                value = Math.Max(Math.Min(value, extent1), extent2);
            }

            return value;
        }

        /// <summary>
        /// Look up the ConfigNode for the named MAS_PAGE.
        /// </summary>
        /// <param name="pageName">Name of the requested page configuration.</param>
        /// <returns>The ConfigNode, or null if it wasn't found.</returns>
        internal static ConfigNode GetPageConfigNode(string pageName)
        {
            ConfigNode[] asPageNodes = GameDatabase.Instance.GetConfigNodes("MAS_PAGE");

            for (int nodeIdx = asPageNodes.Length - 1; nodeIdx >= 0; --nodeIdx)
            {
                string nodeName = string.Empty;
                if (asPageNodes[nodeIdx].TryGetValue("name", ref nodeName) && nodeName == pageName)
                {
                    return asPageNodes[nodeIdx];
                }
            }

            return null;
        }

        /// <summary>
        /// Find the ConfigNode corresponding to a particular module in a part.
        /// </summary>
        /// <param name="part">The part to search</param>
        /// <param name="moduleName">Name of the module</param>
        /// <returns></returns>
        internal static ConfigNode GetPartModuleConfigNode(Part part, string moduleName)
        {
            ConfigNode[] moduleConfigs = part.partInfo.partConfig.GetNodes("MODULE");
            for (int i = 0; i < moduleConfigs.Length; ++i)
            {
                if (moduleConfigs[i].GetValue("name") == moduleName)
                {
                    return moduleConfigs[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Find the ConfigNode corresponding to a particular module.
        /// </summary>
        /// <param name="propName">Name of the prop</param>
        /// <param name="moduleID">ID (index) of the node</param>
        /// <returns></returns>
        internal static ConfigNode GetPropModuleConfigNode(string propName, int moduleID)
        {
            ConfigNode[] dbNodes = GameDatabase.Instance.GetConfigNodes("PROP");

            for (int nodeIdx = dbNodes.Length - 1; nodeIdx >= 0; --nodeIdx)
            {
                if (dbNodes[nodeIdx].GetValue("name") == propName)
                {
                    ConfigNode[] moduleNodes = dbNodes[nodeIdx].GetNodes("MODULE");
                    if (moduleNodes.Length > moduleID)
                    {
                        return moduleNodes[moduleID];
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Search through loaded assemblies to find the specified Type that's
        /// in the specified assemlby.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="fullTypeName"></param>
        /// <returns></returns>
        internal static Type GetExportedType(string assemblyName, string fullTypeName)
        {
            int assyCount = AssemblyLoader.loadedAssemblies.Count;
            for (int assyIndex = 0; assyIndex < assyCount; ++assyIndex )
            {
                AssemblyLoader.LoadedAssembly assy = AssemblyLoader.loadedAssemblies[assyIndex];
                if(assy.name == assemblyName)
                {
                    Type[] exportedTypes = assy.assembly.GetExportedTypes();
                    int typeCount = exportedTypes.Length;
                    for(int typeIndex = 0; typeIndex < typeCount; ++typeIndex)
                    {
                        if(exportedTypes[typeIndex].FullName == fullTypeName)
                        {
                            return exportedTypes[typeIndex];
                        }
                    }
                }
            }
                
            return null;
        }

        /// <summary>
        /// Convert a string to a Color32; supports RasterPropMonitor COLOR_
        /// names.
        /// </summary>
        /// <param name="colorString">String to convert</param>
        /// <param name="comp">Reference to the ASFlightComputer</param>
        /// <returns></returns>
        internal static UnityEngine.Color32 ParseColor32(string colorString, MASFlightComputer comp)
        {
            colorString = colorString.Trim();

            if (colorString.StartsWith("COLOR_"))
            {
                // Using a RasterPropMonitor named color.
                return comp.GetNamedColor(colorString);
            }
            else
            {
                return ConfigNode.ParseColor32(colorString);
            }
        }

        /// <summary>
        /// Remap the source variable from [sourceRange1, sourceRange2] into
        /// the range [destinationRange1, destinationRange2].  Convert to
        /// float as well, since we don't need maximum precision.
        /// </summary>
        /// <param name="sourceVariable"></param>
        /// <param name="sourceRange1"></param>
        /// <param name="sourceRange2"></param>
        /// <param name="destinationRange1"></param>
        /// <param name="destinationRange2"></param>
        /// <returns></returns>
        internal static float Remap(this double sourceVariable, double sourceRange1, double sourceRange2, double destinationRange1, double destinationRange2)
        {
            float iLerp = Mathf.InverseLerp((float)sourceRange1, (float)sourceRange2, (float)sourceVariable);
            return Mathf.Lerp((float)destinationRange1, (float)destinationRange2, iLerp);
        }

        /// <summary>
        /// Temporary worker method to set positions for the LineRenderer.
        /// Unity docs say there's a SetPositions method for that class, but
        /// it is not in Unity 5.2.4f; hopefully KSP 1.2's new Unity flavor
        /// will include it.
        /// </summary>
        /// <param name="lineRenderer"></param>
        /// <param name="numVertices"></param>
        /// <param name="graphPoints"></param>
        internal static void SetPositions(LineRenderer lineRenderer, int numVertices, Vector3[] graphPoints)
        {
            lineRenderer.SetVertexCount(numVertices);
            for (int i = 0; i < numVertices; ++i)
            {
                lineRenderer.SetPosition(i, graphPoints[i]);
            }
        }


        /// <summary>
        /// Computes Stagnation Pressure using gamma (ratio of specific heats)
        /// and Mach number.
        /// Per https://en.wikipedia.org/wiki/Stagnation_pressure
        /// </summary>
        /// <param name="gamma">Ratio of specific heats (CelestialBody.atmosphereAdiabaticIndex)</param>
        /// <param name="M">Mach number (Vessel.mach)</param>
        /// <returns></returns>
        internal static double StagnationPressure(double gamma, double M)
        {
            double term = 1.0 + 0.5 * (gamma - 1.0) * M * M;
            return Math.Pow(term, gamma / (gamma - 1.0));
        }

    }
}
