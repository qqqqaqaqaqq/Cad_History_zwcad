using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Controls;
using System.Xml.Linq;
using ZWCAD;

namespace CadEye.Lib
{
    public class ZwCad_Lib
    {
        /// <summary>
        /// 지더블유 캐드 text 파일 추출
        /// 반환 (string[],string[])
        /// </summary>
        public (List<string>, List<string>) WorkFlow_Zwcad(string path)
        {
            (List<string>, List<string>) sender = (null, null);
            Thread staThread = new Thread(() =>
            {
                ZcadApplication _zwcad = new ZcadApplication();
                _zwcad.Visible = false;
                sender = Cad_Text_Extrude(path, _zwcad);
                Zwcad_Shutdown(_zwcad);
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
            return sender;
        }
        public bool Zwcad_Shutdown(ZcadApplication _zwcad)
        {
            if (_zwcad != null)
            {
                try
                {
                    foreach (ZWCAD.ZcadDocument doc in _zwcad.Documents)
                    {
                        try
                        {
                            Marshal.FinalReleaseComObject(doc.ModelSpace);
                            Marshal.FinalReleaseComObject(doc.Layouts);
                            Marshal.FinalReleaseComObject(doc.Plot);
                            Marshal.FinalReleaseComObject(doc);
                        }
                        catch { }
                    }

                    Marshal.FinalReleaseComObject(_zwcad.Documents);

                    _zwcad.Quit();
                    Marshal.FinalReleaseComObject(_zwcad);
                }
                catch { }

                _zwcad = null;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return true;
        }
        public (List<string>, List<string>) Cad_Text_Extrude(string path, ZcadApplication _zwcad)
        {
            (List<string>, List<string>) autocad_text = (new List<string>(), new List<string>());
            string ex = System.IO.Path.GetExtension(path);
            if (ex.ToUpper() == ".DWG" || ex.ToUpper() == "DXF") { }
            else { return (null, null); }

            var zwcad_in = _zwcad.Documents.Open(path, true);
            var layouts = zwcad_in.Layouts;
            Plot_Check(layouts);
            zwcad_in.Plot.PlotToFile(path);

            foreach (object obj_entity in zwcad_in.ModelSpace)
            {
                Entity_Check(obj_entity, autocad_text);
            }
            Marshal.FinalReleaseComObject(zwcad_in.ModelSpace);
            Marshal.FinalReleaseComObject(zwcad_in.Layouts);
            Marshal.FinalReleaseComObject(zwcad_in.Plot);

            zwcad_in.Close();
            Marshal.FinalReleaseComObject(zwcad_in);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            return autocad_text;
        }
        public void Plot_Check(ZcadLayouts layouts)
        {
            var modelLayout = layouts.Item("Model");
            modelLayout.ConfigName = "ZWCAD PDF(High Quality Print).pc5";
            modelLayout.CanonicalMediaName = "A1";
            modelLayout.PlotWithPlotStyles = true;
            modelLayout.StyleSheet = "monochrome.ctb";
            modelLayout.RefreshPlotDeviceInfo();
            modelLayout.CenterPlot = true;
            modelLayout.PlotRotation = ZcPlotRotation.zc0degrees;
            modelLayout.PlotType = ZcPlotType.zcExtents;
            modelLayout.RefreshPlotDeviceInfo();
        }
        public void Entity_Check(object obj_entity, (List<string>, List<string>) autocad_text)
        {
            if (obj_entity is ZcadEntity zwcad_entity)
            {
                string[] newItems;
                int type = 0;
                string content = "";
                string entityName = zwcad_entity.EntityName.ToUpper();
                switch (entityName)
                {
                    case "ACDBTEXT":
                        var textEntity = (ZcadText)zwcad_entity;
                        content = textEntity.TextString;
                        (content, type) = Text_Convey(content);
                        break;
                    case "ACDBMTEXT":
                        var mtextEntity = (ZcadMText)zwcad_entity;
                        content = mtextEntity.TextString;
                        (content, type) = Text_Convey(content);
                        break;
                }

                switch (type)
                {
                    case 1:
                        newItems = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        autocad_text.Item1.AddRange(newItems);
                        break;
                    case 2:
                        newItems = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        autocad_text.Item2.AddRange(newItems);
                        break;
                    case 0:
                        break;
                }
            }
        }
        private static (string, int) Text_Convey(string text)
        {
            int type = 0;
            if (string.IsNullOrEmpty(text))
                return (text, type);
            try
            {
                text = text.Replace("{", "").Replace("}", "");
                text = Regex.Replace(text, @"\\F[^;]+;", "");
                text = text.Replace(@"\P", "\n");
                text = text.Trim();

                if (Regex.IsMatch(text, "<\\s*TAG\\s*>", RegexOptions.IgnoreCase))
                {
                    text = Regex.Replace(text, "<\\s*/?\\s*TAG\\s*>", "", RegexOptions.IgnoreCase);
                    type = 1;
                }
                else if (Regex.IsMatch(text, "<\\s*REF\\s*>", RegexOptions.IgnoreCase))
                {
                    text = Regex.Replace(text, "<\\s*/?\\s*REF\\s*>", "", RegexOptions.IgnoreCase);
                    type = 2;
                }

                text = text.TrimStart();
                return (text.Trim(), type);
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[Error] {ex.Message}");
                Debug.WriteLine($"[StackTrace] {ex.StackTrace}");
                return (text.Trim(), type); ;
            }
        }
    }
}
