using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XPlane12Plugin.Model;
using XPlane12Plugin.Properties;
using YawGLAPI;

namespace XPlane12Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "X-Plane 12")]
    [ExportMetadata("Version", "2.0")]
    class XPlane12Plugin : Game {


        UdpClient udpClient;
        private Thread readThread;
        private volatile bool running = false;
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 4123);
        IPEndPoint xplaneEndP = new IPEndPoint(IPAddress.Loopback,49000);
        private IMainFormDispatcher dispatcher;
        private IProfileManager controller;
        public string PROCESS_NAME => "X-Plane";
        public int STEAM_ID => 2014780;
        public string AUTHOR => "ItsVRK";
        public bool PATCH_AVAILABLE => false;
        public string Description => Resources.description;

        public Stream Logo => GetStream("logo.png");
        public Stream SmallLogo => GetStream("recent.png");
        public Stream Background => GetStream("wide.png");




        private Dictionary<int, DatarefValue> datarefWatchers = new Dictionary<int, DatarefValue>();



        public LedEffect DefaultLED() {
            return new LedEffect(

           EFFECT_TYPE.KNIGHT_RIDER,
           2,
           new YawColor[] {
                new YawColor(66, 135, 245),
                 new YawColor(80,80,80),
                new YawColor(128, 3, 117),
                new YawColor(110, 201, 12),
                },
           0.7f);
        }

        public List<Profile_Component> DefaultProfile() {

            return new List<Profile_Component>();
        }

        public void Exit() {
            udpClient.Close();
            udpClient = null;
            running = false;
            //readThread.Abort();



        }

        public string[] GetInputData() {
            List<string> strings = new List<string>();

            foreach(var item in datarefWatchers.Values)
            {
                strings.Add(item.InputName);
            }

            return strings.ToArray();
        }


        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.controller = controller;

            DatarefValue[] objectFileData;
            dispatcher.GetObjectFile("xpl12", out objectFileData);
            if (objectFileData != null)
            {
                SetupDatarefs(objectFileData);
            }
        }

        private void SetupDatarefs(DatarefValue[] objectFileData)
        {
            datarefWatchers.Clear();
            int i = 0;

            foreach (var dataref in objectFileData)
            {
                datarefWatchers.Add(i++, dataref);
            }
        }

        public void Init() {

            var pConfig = dispatcher.GetConfigObject<Config>();
            running = true;
            udpClient = new UdpClient(5555);
            udpClient.Client.ReceiveTimeout = 5000;
            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();

            RequestDataFromXPlane();
            

        }
        private async void RequestDataFromXPlane()
        {
            foreach(var dataref in datarefWatchers)
            {

                byte[] bytes = new byte[413];
                byte[] b_rref = Encoding.UTF8.GetBytes(dataref.Value.Dataref + '\0');

                Buffer.BlockCopy(Encoding.UTF8.GetBytes("RREF\0"), 0, bytes, 0, 5);
                Buffer.BlockCopy(BitConverter.GetBytes(50), 0, bytes, 5, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(dataref.Key), 0, bytes, 9, 4);
                Buffer.BlockCopy(b_rref, 0, bytes, 13, b_rref.Length);
                await udpClient.SendAsync(bytes,bytes.Length,xplaneEndP);
            }
        }
        private void ReadFunction() {
            while (running) {
                try
                {
                    byte[] rawData = udpClient.Receive(ref remote);
                    ParseResponse(rawData);
                    int index = 0;
                    foreach(var dataref in datarefWatchers.Values)
                    {
                        controller.SetInput(index++, dataref.Value);
                    }

                } catch(Exception) { }


            }

        }
        private void ParseResponse(byte[] bytes)
        {
            int cnt = (bytes.Length - 5) / 8;
            for (int i = 0; i < cnt; i++)
            {
                int index = BitConverter.ToInt32(bytes, 5 + (i * 8));
                float val = BitConverter.ToSingle(bytes, 9 + (i * 8));

                if (datarefWatchers.ContainsKey(index)) datarefWatchers[index].Value = val;
            }
           
        }
        public void PatchGame()
        {
            return;
        }

  
        public void SendCommand(string command)
        {
            byte[] bytes = new byte[6 + command.Length];
            Buffer.BlockCopy(ASCIIEncoding.ASCII.GetBytes("CMND\0"), 0, bytes, 0, 5);
            Buffer.BlockCopy(ASCIIEncoding.ASCII.GetBytes(command), 0, bytes, 5, command.Length);
            bytes[bytes.Length - 1] = 0x00;
            udpClient.Send(bytes, bytes.Length,xplaneEndP);
        }

        Stream GetStream(string resourceName)
        {
            var assembly = GetType().Assembly;
            var rr = assembly.GetManifestResourceNames();
            string fullResourceName = $"{assembly.GetName().Name}.Resources.{resourceName}";
            return assembly.GetManifestResourceStream(fullResourceName);
        }

        public Type GetConfigBody()
        {
            return typeof(Config);
        }
        #region features
        public Dictionary<string, ParameterInfo[]> GetFeatures()
        {
            var ret = new Dictionary<string, ParameterInfo[]>();

            string[] methodNames = new string[] {
                nameof(Pause),nameof(ResetVR),nameof(ToggleVR), nameof(ResetRunway), nameof(ToggleRegular),
                nameof(ThrottleDown),nameof(ThrottleUp),nameof(SendCommand) };

            foreach (string methodName in methodNames)
            {
                var method = this.GetType().GetMethod(methodName);
                ret.Add(method.Name, method.GetParameters());
            }
            return ret;

        }


        public void ResetRunway()
        {
            SendCommand("sim/operation/reset_to_runway");
        }

        public void ToggleVR()
        {
            SendCommand("sim/VR/toggle_vr");
        }
        public void ResetVR()
        {
            SendCommand("sim/VR/general/reset_view");
        }
        public void ThrottleUp()
        {
            SendCommand("sim/engines/throttle_up");
        }
        public void ThrottleDown()
        {
            SendCommand("sim/engines/throttle_down");
        }
        public void ToggleRegular()
        {
            SendCommand("sim/flight_controls/brakes_toggle_regular");
        }
        public void Pause()
        {
            SendCommand(@"sim/operation/pause_toggle");
        }
        #endregion
    }
}
