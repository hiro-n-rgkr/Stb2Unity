﻿using System.Collections.Generic;
using System.Xml.Linq;
using SFB;
using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public partial class STBReader:MonoBehaviour {
    public Material material;
    static List<Vector3> m_stbNodes = new List<Vector3>();
    static List<int> m_vertexIDs = new List<int>();
    
    static List<int> m_xRcColumnId = new List<int>();
    static List<int> m_xRcColumnDepth = new List<int>();
    static List<int> m_xRcColumnWidth = new List<int>();
    static List<int> m_xStColumnId = new List<int>();
    static List<string> m_xStColumnShape = new List<string>();
    static List<int> m_xRcBeamId = new List<int>();
    static List<int> m_xRcBeamDepth = new List<int>();
    static List<int> m_xRcBeamWidth = new List<int>();
    static List<int> m_xStBeamId = new List<int>();
    static List<string> m_xStBeamShape = new List<string>();
    static List<int> m_xStBraceId = new List<int>();
    static List<string> m_xStBraceShape = new List<string>();
    static List<string> m_xStName = new List<string>();
    static List<float> m_xStParamA = new List<float>();
    static List<float> m_xStParamB = new List<float>();
    static List<string> m_xStType = new List<string>();
    static List<Mesh> m_shapeMesh = new List<Mesh>();

    [MenuItem("Stb2U/Open .stb File")]
    public static void StbUI() {
        int i = 0;
        XDocument xDoc = GetStbFileData();
        GetStbNodes(xDoc, m_stbNodes, m_vertexIDs);
        MakeSlabObjs(xDoc);
        // StbSecColumn_RC の取得
        var xRcColumns = xDoc.Root.Descendants("StbSecColumn_RC");
        foreach (var xRcColumn in xRcColumns) {
            m_xRcColumnId.Add((int)xRcColumn.Attribute("id"));
            var xFigure = xRcColumn.Element("StbSecFigure");

            // 子要素が StbSecRect か StbSecCircle を判定
            if (xFigure.Element("StbSecRect") != null) {
                m_xRcColumnDepth.Add((int)xFigure.Element("StbSecRect").Attribute("DY"));
                m_xRcColumnWidth.Add((int)xFigure.Element("StbSecRect").Attribute("DX"));
            }
            else {
                m_xRcColumnDepth.Add((int)xFigure.Element("StbSecCircle").Attribute("D"));
                m_xRcColumnWidth.Add(0); // Circle と判定用に width は 0
            }
        }
        // StbSecColumn_S の取得
        var xStColumns = xDoc.Root.Descendants("StbSecColumn_S");
        foreach (var xSecSColumn in xStColumns) {
            m_xStColumnId.Add((int)xSecSColumn.Attribute("id"));
            m_xStColumnShape.Add((string)xSecSColumn.Element("StbSecSteelColumn").Attribute("shape"));
        }
        // StbSecBeam_RC の取得
        var xRcBeams = xDoc.Root.Descendants("StbSecBeam_RC");
        foreach (var xRcBeam in xRcBeams) {
            m_xRcBeamId.Add((int)xRcBeam.Attribute("id"));
            var xFigure = xRcBeam.Element("StbSecFigure");

            // 子要素が StbSecHaunch か StbSecStraight を判定
            if (xFigure.Element("StbSecHaunch") != null) {
                m_xRcBeamDepth.Add((int)xFigure.Element("StbSecHaunch").Attribute("depth_center"));
                m_xRcBeamWidth.Add((int)xFigure.Element("StbSecHaunch").Attribute("width_center"));
            }
            else {
                m_xRcBeamDepth.Add((int)xFigure.Element("StbSecStraight").Attribute("depth"));
                m_xRcBeamWidth.Add((int)xFigure.Element("StbSecStraight").Attribute("width"));
            }
        }
        // StbSecBeam_S の取得
        var xStBeams = xDoc.Root.Descendants("StbSecBeam_S");
        foreach (var xStBeam in xStBeams) {
            m_xStBeamId.Add((int)xStBeam.Attribute("id"));
            m_xStBeamShape.Add((string)xStBeam.Element("StbSecSteelBeam").Attribute("shape"));
        }
        // StbSecBrace_S の取得
        var xStBraces = xDoc.Root.Descendants("StbSecBrace_S");
        foreach (var xStBrace in xStBraces) {
            m_xStBraceId.Add((int)xStBrace.Attribute("id"));
            m_xStBraceShape.Add((string)xStBrace.Element("StbSecSteelBrace").Attribute("shape"));
        }
        // S断面形状の取得
        i = 0;
        string[,] SteelSecName = GetSteelSecNameArray();
        while (i < SteelSecName.GetLength(0)) {
            GetStbSteelSection(xDoc, SteelSecName[i, 0], SteelSecName[i, 1]);
            i++;
        } 
        // 断面の生成
        i = 0;
        string[,] memberName = GetMemberNameArray();
        while (i < memberName.GetLength(0)) {
            MakeElementMesh(xDoc, memberName[i, 0], memberName[i, 1]);
            i++;
        }
    }

    static XDocument GetStbFileData() {
        var extensions = new[] {
            new ExtensionFilter("ST-Bridge Files", "stb", "STB" ),
            new ExtensionFilter("All Files", "*" ),
        };
        string paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", extensions, true)[0];
        XDocument xDoc = XDocument.Load(paths);
        return (xDoc);
    }

    static void GetStbNodes(XDocument xDoc, List<Vector3> stbNodes, List<int> vertexIds) {
        float xPos, yPos, zPos;
        int nodeId;
        var xNodes = xDoc.Root.Descendants("StbNode");

        foreach (var xNode in xNodes) {
            // unity は 1 が 1m なので1000で割ってる
            xPos = (float) xNode.Attribute("x") / 1000;
            yPos = (float) xNode.Attribute("z") / 1000; // unityは Z-Up
            zPos = (float) xNode.Attribute("y") / 1000;
            nodeId = (int) xNode.Attribute("id");

            stbNodes.Add(new Vector3(xPos, yPos, zPos));
            vertexIds.Add(nodeId);
        }
    }

    static string[,] GetSteelSecNameArray() {
        string[,] steelSecNameArray = new string[7, 2] {
            {"StbSecRoll-H", "H"},
            {"StbSecBuild-H", "H"},
            {"StbSecRoll-BOX", "BOX"},
            {"StbSecBuild-BOX", "BOX"},
            {"StbSecPipe", "Pipe"},
            {"StbSecRoll-L", "L"},
            {"StbSecRoll-Bar", "Bar"}
        };
        return (steelSecNameArray);
    }

    static string[,] GetMemberNameArray() {
        string[,] memberNameArray = new string[5, 2] {
            {"StbColumn", "Column"},
            {"StbGirder", "Girder"},
            {"StbPost", "Post"},
            {"StbBeam", "Beam"},
            {"StbBrace", "Brace"}
        };
        return (memberNameArray);
    }
}
