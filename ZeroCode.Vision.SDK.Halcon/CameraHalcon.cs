using System;
using System.Collections.Generic;
using System.Diagnostics;
using HalconDotNet;

namespace ZeroCode.Vision.SDK.Halcon
{
    public class CameraHalcon : BaseCamera
    { 
        public override bool AutoBalanceWhite()
        {
            throw new NotImplementedException();
        }

        public override bool CloseDevice()
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public override List<string> GetCameraEnum()
        {
            throw new NotImplementedException();
        }

        public override bool GetExpouseTime(out float value)
        {
            throw new NotImplementedException();
        }

        public override bool GetGain(out float gain)
        {
            throw new NotImplementedException();
        }

        public override bool GetLineStatus(EIOLines line, out ELineStatus lineStatus)
        {
            throw new NotImplementedException();
        }

        public override bool GetTriggerDelay(out float delay)
        {
            throw new NotImplementedException();
        }

        public override bool GetTriggerFliter(out float flitertime)
        {
            throw new NotImplementedException();
        }

        public override bool GetTriggerMode(out ETriggerMode mode, out ETriggerSource hardTriggerModel)
        {
            throw new NotImplementedException();
        }

        public override bool GetTriggerPolarity(out ETriggerPolarity polarity)
        {
            throw new NotImplementedException();
        }


        public override bool InitizeDevice(string CamSN)
        {
            throw new NotImplementedException();
        }

        public override bool SetExpouseTime(float value)
        {
            throw new NotImplementedException();
        }

        public override bool SetGain(float gain)
        {
            throw new NotImplementedException();
        }

        public override bool SetLineMode(EIOLines line, ELineMode mode)
        {
            throw new NotImplementedException();
        }

        public override bool SetLineStatus(EIOLines line, ELineStatus linestatus)
        {
            throw new NotImplementedException();
        }

        public override bool SetTriggerDelay(float delay)
        {
            throw new NotImplementedException();
        }

        public override bool SetTriggerFliter(float flitertime)
        {
            throw new NotImplementedException();
        }

        public override bool SetTriggerMode(ETriggerMode mode, ETriggerSource triggerEnum = ETriggerSource.Line0)
        {
            throw new NotImplementedException();
        }

        public override bool SetTriggerPolarity(ETriggerPolarity polarity)
        {
            throw new NotImplementedException();
        }

        public override bool SoftTrigger()
        {
            throw new NotImplementedException();
        }

        public override bool StartGrabbing()
        {
            throw new NotImplementedException();
        }

        public override bool StopGrabbing()
        {
            throw new NotImplementedException();
        }
    }
}
