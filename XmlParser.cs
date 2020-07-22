using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TestTaskForMacro
{
    static class XmlParser
    {
        public static List<Camera> Parser(string fileName)
        {
            List<Camera> cameras = new List<Camera>();
            Camera bufCamera = new Camera();

            using (StreamReader sr = new StreamReader(fileName))
            {
                int index = 0, indexOfEnd = 0;
                string s = sr.ReadToEnd();
                indexOfEnd = s.IndexOf("</Channels>", index);
                while(index < indexOfEnd)
                {
                    if ((bufCamera = ParserHelper(ref index, s)) != null)
                    {
                        cameras.Add(bufCamera);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return cameras;
        }
        private static Camera ParserHelper(ref int index, string s)
        {
            string sBuf = "";
            Camera bufCamera = new Camera();

            index = s.IndexOf("<ChannelInfo Id=", index);
            if (index == -1)
            {
                return null;
            }

            bufCamera.Id = s.Substring(index += 17, 36);
            index += 44;
            while (s[index] != '"')
            {
                sBuf += s[index];
                index++;
            }
            bufCamera.Name = sBuf;
            
            return bufCamera;
        }
    }
}
