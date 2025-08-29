using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
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
            try
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
            catch (Exception)
            {
                return (null, null);
            }
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
            try
            {
                (List<string>, List<string>) autocad_text = (new List<string>(), new List<string>());
                string ex = System.IO.Path.GetExtension(path);
                if (ex.ToUpper() == ".DWG" || ex.ToUpper() == "DXF") { }
                else { return (null, null); }

                if (!System.IO.File.Exists(path))
                {
                    Debug.WriteLine("파일 오류: " + path);
                    return (null, null);
                }

                var zwcad_in = _zwcad.Documents.Open(path, true);
                var layouts = zwcad_in.Layouts;

                if (layouts == null) { return (null, null); }

                Plot_Check(layouts);
                zwcad_in.Plot.PlotToFile(path);


                var buffer1 = new ConcurrentBag<string>();
                var buffer2 = new ConcurrentBag<string>();

                Parallel.ForEach(zwcad_in.ModelSpace.Cast<object>(), obj_entity =>
                {
                    var zwcad_entity = obj_entity as ZcadEntity;
                    if (zwcad_entity == null) return;
                    string content = "";
                    int type = 0;
                    string entityName = zwcad_entity.EntityName.ToUpper();

                    switch (entityName)
                    {
                        case "ACDBTEXT":
                            content = ((ZcadText)zwcad_entity).TextString;
                            (content, type) = Text_Convey(content);
                            break;
                        case "ACDBMTEXT":
                            content = ((ZcadMText)zwcad_entity).TextString;
                            (content, type) = Text_Convey(content);
                            break;
                        default:
                            return;
                    }

                    if (type == 0) return;

                    var newItems = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (type == 1)
                    {
                        foreach (var item in newItems)
                            buffer1.Add(item);
                    }
                    else if (type == 2)
                    {
                        foreach (var item in newItems)
                            buffer2.Add(item);
                    }
                });

                autocad_text.Item1.AddRange(buffer1);
                autocad_text.Item2.AddRange(buffer2);


                // 가비지로 회수 순서 지킬 것
                Marshal.FinalReleaseComObject(zwcad_in.ModelSpace);
                Marshal.FinalReleaseComObject(zwcad_in.Layouts);
                Marshal.FinalReleaseComObject(zwcad_in.Plot);

                zwcad_in.Close();
                Marshal.FinalReleaseComObject(zwcad_in);
                GC.Collect();
                GC.WaitForPendingFinalizers();

                return autocad_text;

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cad_Text_Extrude : Error {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                return (null, null);
            }
        }
        public void Plot_Check(ZcadLayouts layouts)
        {
            try
            {
                var modelLayout = layouts.Item("Model");
                var plotters = modelLayout.GetPlotDeviceNames();
                string device = null;  // null로 초기화

                foreach (string plotter in plotters)
                {
                    if (plotter == "ZWCAD PDF(High Quality Print).pc5")
                    {
                        device = plotter;
                        break;
                    }
                }

                if (device == null)
                {
                    foreach (string plotter in plotters)
                    {
                        if (plotter == "DWG To PDF.pc5")
                        {
                            device = plotter;
                            break;
                        }
                    }
                }

                if (device == null)
                {
                    foreach (string plotter in plotters)
                    {
                        if (plotter == "ZWCAD PDF(General Documentation).pc5")
                        {
                            device = plotter;
                            break;
                        }
                    }
                }

                Debug.WriteLine($"plot {device}");
                modelLayout.ConfigName = device;
                modelLayout.RefreshPlotDeviceInfo();
                modelLayout.CanonicalMediaName = "A1";
                modelLayout.PlotWithPlotStyles = true;
                modelLayout.CenterPlot = true;
                modelLayout.PlotRotation = ZcPlotRotation.zc0degrees;
                modelLayout.PlotType = ZcPlotType.zcExtents;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Plot_Check : Error {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
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
                text = Regex.Replace(text, @"\\[^;]+;", "");
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
                return (text.Trim(), type);
            }
        }
    }
}
