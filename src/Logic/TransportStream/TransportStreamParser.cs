﻿using Nikse.SubtitleEdit.Logic.BluRaySup;
using Nikse.SubtitleEdit.Logic.VobSub;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Nikse.SubtitleEdit.Logic.TransportStream
{
    /// <summary>
    /// MPEG transport stream parser
    /// </summary>
    public class TransportStreamParser
    {
        public int NumberOfNullPackets { get; private set; }
        public long TotalNumberOfPackets { get; private set; }
        public long TotalNumberOfPrivateStream1 { get; private set; }
        public long TotalNumberOfPrivateStream1Continuation0 { get; private set; }
        public List<int> SubtitlePacketIds { get; private set; }
        public List<Packet> SubtitlePackets { get; private set; }
        public List<Packet> ProgramAssociationTables { get; private set; }
        private Dictionary<int, List<DvbSubPes>> SubtitlesLookup { get; set; }
        private Dictionary<int, List<TransportStreamSubtitle>> DvbSubtitlesLookup { get; set; }
        public bool IsM2TransportStream { get; private set; }
        public ulong FirstVideoPts { get; private set; }

        public void ParseTsFile(string fileName)
        {
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            ParseTsFile(fs);
            fs.Close();
        }

        /// <summary>
        /// Can be used with e.g. MemoryStream or FileStream
        /// </summary>
        /// <param name="ms">Input stream</param>
        public void ParseTsFile(Stream ms)
        {
            bool firstVideoPtsFound = false;
            IsM2TransportStream = false;
            NumberOfNullPackets = 0;
            TotalNumberOfPackets = 0;
            TotalNumberOfPrivateStream1 = 0;
            TotalNumberOfPrivateStream1Continuation0 = 0;
            SubtitlePacketIds = new List<int>();
            SubtitlePackets = new List<Packet>();
            ProgramAssociationTables = new List<Packet>();
            ms.Position = 0;
            int packetLength = 188;
            DetectFormat(ms);
            var packetBuffer = new byte[packetLength];
            var m2tsTimeCodeBuffer = new byte[4];
            long position = 0;

            // check for Topfield .rec file
            ms.Seek(position, SeekOrigin.Begin);
            ms.Read(m2tsTimeCodeBuffer, 0, 3);
            if (m2tsTimeCodeBuffer[0] == 0x54 && m2tsTimeCodeBuffer[1] == 0x46 && m2tsTimeCodeBuffer[2] == 0x72)
                position = 3760;


            while (position < ms.Length)
            {
                ms.Seek(position, SeekOrigin.Begin);

                if (IsM2TransportStream)
                {
                    ms.Read(m2tsTimeCodeBuffer, 0, m2tsTimeCodeBuffer.Length);
                    var tc = (m2tsTimeCodeBuffer[0]<< 24) | (m2tsTimeCodeBuffer[1] << 16) | (m2tsTimeCodeBuffer[2] << 8) | (m2tsTimeCodeBuffer[3]);
                    position += m2tsTimeCodeBuffer.Length;
                }

                ms.Read(packetBuffer, 0, packetLength);
                byte syncByte = packetBuffer[0];
                if (syncByte == Packet.SynchronizationByte)
                {
                    var packet = new Packet(packetBuffer);

                    if (packet.IsNullPacket)
                    {
                        NumberOfNullPackets++;
                    }
                    else if (!firstVideoPtsFound && packet.IsVideoStream)
                    {
                        if (packet.Payload != null && packet.Payload.Length > 10)
                        {
                            int presentationTimeStampDecodeTimeStampFlags = packet.Payload[7] >> 6;
                            if (presentationTimeStampDecodeTimeStampFlags == Helper.B00000010 ||
                                presentationTimeStampDecodeTimeStampFlags == Helper.B00000011)
                            {
                                FirstVideoPts = (ulong)packet.Payload[9 + 4] >> 1;
                                FirstVideoPts += (ulong)packet.Payload[9 + 3] << 7;
                                FirstVideoPts += (ulong)(packet.Payload[9 + 2] & Helper.B11111110) << 14;
                                FirstVideoPts += (ulong)packet.Payload[9 + 1] << 22;
                                FirstVideoPts += (ulong)(packet.Payload[9 + 0] & Helper.B00001110) << 29;
                                firstVideoPtsFound = true;
                            }
                        }
                    }
                    else if (packet.IsProgramAssociationTable)
                    {
                        //var sb = new StringBuilder();
                        //sb.AppendLine("PacketNo: " + TotalNumberOfPackets + 1);
                        //sb.AppendLine("PacketId: " + packet.PacketId);
                        //sb.AppendLine();
                        //sb.AppendLine("TransportErrorIndicator: " + packet.TransportErrorIndicator);
                        //sb.AppendLine("PayloadUnitStartIndicator: " + packet.PayloadUnitStartIndicator);
                        //sb.AppendLine("TransportPriority: " + packet.TransportPriority);
                        //sb.AppendLine("ScramblingControl: " + packet.ScramblingControl);
                        //sb.AppendLine("AdaptationFieldExist: " + packet.AdaptationFieldControl);
                        //sb.AppendLine("ContinuityCounter: " + packet.ContinuityCounter);
                        //sb.AppendLine();
                        //if (packet.AdaptationField != null)
                        //{
                        //sb.AppendLine("AdaptationFieldLength: " + packet.AdaptationField.Length);
                        //sb.AppendLine("DiscontinuityIndicator: " + packet.AdaptationField.DiscontinuityIndicator);
                        //sb.AppendLine("RandomAccessIndicator: " + packet.AdaptationField.RandomAccessIndicator);
                        //sb.AppendLine("ElementaryStreamPriorityIndicator: " + packet.AdaptationField.ElementaryStreamPriorityIndicator);
                        //sb.AppendLine("PcrFlag: " + packet.AdaptationField.PcrFlag);
                        //sb.AppendLine("OpcrFlag: " + packet.AdaptationField.OpcrFlag);
                        //sb.AppendLine("SplicingPointFlag: " + packet.AdaptationField.SplicingPointFlag);
                        //sb.AppendLine("TransportPrivateDataFlag: " + packet.AdaptationField.TransportPrivateDataFlag);
                        //sb.AppendLine("AdaptationFieldExtensionFlag: " + packet.AdaptationField.AdaptationFieldExtensionFlag);
                        //sb.AppendLine();
                        //}
                        //sb.AppendLine("TableId: " + packet.ProgramAssociationTable.TableId);
                        //sb.AppendLine("SectionLength: " + packet.ProgramAssociationTable.SectionLength);
                        //sb.AppendLine("TransportStreamId: " + packet.ProgramAssociationTable.TransportStreamId);
                        //sb.AppendLine("VersionNumber: " + packet.ProgramAssociationTable.VersionNumber);
                        //sb.AppendLine("CurrentNextIndicator: " + packet.ProgramAssociationTable.CurrentNextIndicator);
                        //sb.AppendLine("SectionNumber: " + packet.ProgramAssociationTable.SectionNumber);
                        //sb.AppendLine("LastSectionNumber: " + packet.ProgramAssociationTable.LastSectionNumber);
                        //ProgramAssociationTables.Add(packet);
                    }
                    else if (packet.IsPrivateStream1 || SubtitlePacketIds.Contains(packet.PacketId))
                    {
                        TotalNumberOfPrivateStream1++;

                        SubtitlePackets.Add(packet);

                        if (!SubtitlePacketIds.Contains(packet.PacketId))
                        {
                            SubtitlePacketIds.Add(packet.PacketId);
                        }
                        if (packet.ContinuityCounter == 0)
                        {
                            TotalNumberOfPrivateStream1Continuation0++;

                            int pesExtensionlength = 0;
                            if (12 + packet.AdaptionFieldLength < packetBuffer.Length)
                                pesExtensionlength = 0xFF & packetBuffer[12 + packet.AdaptionFieldLength];
                            int pesOffset = 13 + packet.AdaptionFieldLength + pesExtensionlength;
                            bool isTeletext = (pesExtensionlength == 0x24 && (0xFF & packetBuffer[pesOffset]) >> 4 == 1);

                            // workaround uk freesat teletext
                            if (!isTeletext)
                                isTeletext = (pesExtensionlength == 0x24 && (0xFF & packetBuffer[pesOffset]) == 0x99);

                            if (!isTeletext)
                            {

                            }
                        }
                    }
                    TotalNumberOfPackets++;
                    position += packetLength;
                }
                else
                {
                    position++;
                }
            }

            if (IsM2TransportStream)
            {
                DvbSubtitlesLookup = new Dictionary<int, List<TransportStreamSubtitle>>();
                SubtitlesLookup = new Dictionary<int, List<DvbSubPes>>();
                foreach (int pid in SubtitlePacketIds)
                {
                    var bdMs = new MemoryStream();
                    var list = MakeSubtitlePesPackets(pid);
                    var startMsList = new List<ulong>();
                    var endMsList = new List<ulong>();
                    foreach (var item in list)
                    {
                        bdMs.Write(item.DataBuffer, 0, item.DataBuffer.Length);
                        if (item.DataBuffer[0] == 0x16)
                        {
                            if (startMsList.Count <= endMsList.Count)
                                startMsList.Add(item.PresentationTimeStampToMilliseconds());
                            else
                                endMsList.Add(item.PresentationTimeStampToMilliseconds());
                        }
                        //else if (item.DataBuffer[0] == 0x80)
                        //{ //TODO Load bd sub after 0x80, so we can be sure to get correct time code???
                        //    endMsList.Add(item.PresentationTimeStampToMilliseconds() / 90);
                        //}
                    }
                    SubtitlesLookup.Add(pid, list);

                    bdMs.Position = 0;
                    var sb = new StringBuilder();
                    var bdList = BluRaySupParser.ParseBluRaySup(bdMs, sb, true);
                    if (bdList.Count > 0)
                    {
                        var subList = new List<TransportStreamSubtitle>();
                        for (int k=0; k < bdList.Count; k++)
                        {
                            var bdSup = bdList[k];
                            ulong startMs = 0;
                            if (k < startMsList.Count)
                                startMs = startMsList[k];
                            ulong endMs = 0;
                            if (k < endMsList.Count)
                                endMs = endMsList[k];
                            subList.Add(new TransportStreamSubtitle(bdSup, startMs, endMs, (ulong)((FirstVideoPts + 45) / 90.0)));
                        }
                        DvbSubtitlesLookup.Add(pid, subList);
                    }
                }
                SubtitlePacketIds.Clear();
                foreach (int key in DvbSubtitlesLookup.Keys)
                    SubtitlePacketIds.Add(key);
                SubtitlePacketIds.Sort();
                return;
            }


            // check for SubPictureStreamId = 32
            SubtitlesLookup = new Dictionary<int,List<DvbSubPes>>();
            foreach (int pid in SubtitlePacketIds)
            {
                var list = MakeSubtitlePesPackets(pid);
                bool hasImageSubtitles = false;
                foreach (var item in list)
                {
                    if (item.IsDvbSubpicture)
                    {
                        hasImageSubtitles = true;
                        break;
                    }
                }
                if (hasImageSubtitles)
                {
                    SubtitlesLookup.Add(pid, list);
                }
            }
            SubtitlePacketIds.Clear();
            foreach (int key in SubtitlesLookup.Keys)
                SubtitlePacketIds.Add(key);
            SubtitlePacketIds.Sort();


            // Merge packets and set start/end time
            DvbSubtitlesLookup = new Dictionary<int, List<TransportStreamSubtitle>>();
            ulong firstVideoMs = (ulong)((FirstVideoPts + 45) / 90.0);
            foreach (int pid in SubtitlePacketIds)
            {
                var subtitles = new List<TransportStreamSubtitle>();
                var list = GetSubtitlePesPackets(pid);
                for (int i = 0; i < list.Count; i++)
                {
                    var pes = list[i];
                    pes.ParseSegments();
                    if (pes.ObjectDataList.Count > 0)
                    {
                        var sub = new TransportStreamSubtitle();
                        sub.StartMilliseconds = pes.PresentationTimeStampToMilliseconds();
                        sub.Pes = pes;
                        if (i + 1 < list.Count && list[i + 1].PresentationTimeStampToMilliseconds() > 25)
                            sub.EndMilliseconds = list[i + 1].PresentationTimeStampToMilliseconds() - 25;
                        if (sub.EndMilliseconds < sub.StartMilliseconds)
                            sub.EndMilliseconds = sub.StartMilliseconds + 3500;
                        subtitles.Add(sub);
                        if (sub.StartMilliseconds < firstVideoMs)
                            firstVideoMs = sub.StartMilliseconds;
                    }
                }
                foreach (var s in subtitles)
                {
                    s.OffsetMilliseconds = firstVideoMs;
                }
                DvbSubtitlesLookup.Add(pid, subtitles);
            }
            SubtitlePacketIds.Clear();
            foreach (int key in DvbSubtitlesLookup.Keys)
            {
                if (DvbSubtitlesLookup[key].Count > 0)
                    SubtitlePacketIds.Add(key);
            }
            SubtitlePacketIds.Sort();
        }

        public List<TransportStreamSubtitle> GetDvbSubtitles(int packetId)
        {
            if (DvbSubtitlesLookup.ContainsKey(packetId))
                return DvbSubtitlesLookup[packetId];
            return null;
        }

        private List<DvbSubPes> MakeSubtitlePesPackets(int packetId)
        {
            var list = new List<DvbSubPes>();
            int last = -1;
            var packetList = new List<Packet>();
            foreach (Packet packet in SubtitlePackets)
            {
                if (packet.PacketId == packetId)
                {
                    if (packet.PayloadUnitStartIndicator)
                    {
                        if (packetList.Count > 0)
                            AddPesPacket(list, packetList);
                        packetList = new List<Packet>();
                    }
                    if (packet.Payload != null && last != packet.ContinuityCounter)
                        packetList.Add(packet);
                    last = packet.ContinuityCounter;
                }
            }
            if (packetList.Count > 0)
                AddPesPacket(list, packetList);
            return list;
        }

        public List<DvbSubPes> GetSubtitlePesPackets(int packetId)
        {
            if (SubtitlesLookup.ContainsKey(packetId))
                return SubtitlesLookup[packetId];
            return null;
        }

        private static void AddPesPacket(List<DvbSubPes> list, List<Packet> packetList)
        {
            int bufferSize = 0;
            foreach (Packet p in packetList)
                bufferSize += p.Payload.Length;
            var pesData = new byte[bufferSize];
            int pesIndex = 0;
            foreach (Packet p in packetList)
            {
                Buffer.BlockCopy(p.Payload, 0, pesData, pesIndex, p.Payload.Length);
                pesIndex += p.Payload.Length;
            }

            DvbSubPes pes;
            if (VobSubParser.IsMpeg2PackHeader(pesData))
            {
                pes = new DvbSubPes(pesData, Mpeg2Header.Length);
            }
            else if (VobSubParser.IsPrivateStream1(pesData, 0))
            {
                pes = new DvbSubPes(pesData, 0);
            }
            else
            {
                pes = new DvbSubPes(pesData, 0);
            }
            list.Add(pes);
        }

        private void DetectFormat(Stream ms)
        {
            if (ms.Length > 192 + 192 + 5)
            {
                ms.Seek(0, SeekOrigin.Begin);
                var buffer = new byte[192 + 192 + 5];
                ms.Read(buffer, 0, buffer.Length);
                if (buffer[0] == Packet.SynchronizationByte && buffer[188] == Packet.SynchronizationByte)
                    return;
                if (buffer[4] == Packet.SynchronizationByte && buffer[192 + 4] == Packet.SynchronizationByte && buffer[192 + 192 + 4] == Packet.SynchronizationByte)
                {
                    IsM2TransportStream = true;
                }
            }
        }

    }
}
