﻿#define TEST
using System;
using System.Collections.Generic;
using System.IO;
//using System.Linq;
using System.Reflection;
using System.Net;
//using System.Text;
using System.Threading;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;


namespace PointAwarder {
    [ApiVersion(1, 23)]
    public class NameValidator : TerrariaPlugin {

#if !TEST
        private String url = "http://www.pedguin.com/";
#else
        private String url = "http://localhost:8080/";
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

        public NameValidator(Main game) : base(game) {
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
            Commands.ChatCommands.Add(new Command("pedguinServer.award", awardStart, "award"));
            Commands.ChatCommands.Add(new Command("pedguinServer.admin", promoteStart, "promote"));
            Commands.ChatCommands.Add(new Command("pedguinServer.admin", demoteStart, "demote"));
            AccountHooks.AccountCreate += OnRegister;
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
        }

        private void awardStart(CommandArgs args) {
            Thread awardThread = new Thread(x => { award(args); });
            awardThread.Start();
        }

        private void award(CommandArgs args) {

            int pointsToSend;

            if (args.Parameters.Count != 2) {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /award PlayerToAward PointsToAward", 255, 0, 0);
            } else if (!Int32.TryParse(args.Parameters[1], out pointsToSend)){
                args.Player.SendMessage("Invalid amount of points: not a number", 255, 0, 0);
            } else if (pointsToSend > 20) {             //change if max amount of points changes
                args.Player.SendMessage("You can't award so many points!", 255, 0, 0);
            } else {
                String awarderUUID = args.Player.UUID;
                List<TSPlayer> awardedPlayer = TShock.Utils.FindPlayer(args.Parameters[0]);
                if(awardedPlayer.Count == 0){
                    args.Player.SendErrorMessage("Invalid player!");
                } else if(awardedPlayer.Count > 1) {
                    args.Player.SendErrorMessage("More than one (" + args.Parameters.Count + ") player matched!");
                } else {
                    String awardedUUID = awardedPlayer[0].UUID;

#if TEST
                    Console.WriteLine("AwardRequest start");
#endif
                    WebRequest request = WebRequest.Create(url + "remoteAward");
                    request.Method = "POST";
                    byte[] awarderUUIDbytes = awarderUUID.ToByteArray();
                    byte[] awardedUUIDbytes = awardedUUID.ToByteArray();
                    byte[] byteArray = new byte[42];
                    //first 20 chars of awarder UUID
                    for (int i = 0, j = 0; i < 20; i++, j++) {
                        byteArray[i] = awarderUUIDbytes[j];
                    }
                    //first 20 chars of awarded UUID
                    for (int i = 20, j = 0; i < 40; i++, j++) {
                        byteArray[i] = awardedUUIDbytes[j];
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
                    byte[] responseBytes = new byte[responseStream.Length];
                    responseStream.Read(responseBytes, 0, (int)responseStream.Length);
                    responseStream.Close();
                    response.Close();
#if TEST
                    Console.WriteLine("AwardRequest end");
#endif

                    switch(responseBytes[0]) {
                        case 0: 
                            args.Player.SendMessage("Points awarded!", 255, 128, 0);
                            awardedPlayer[0].SendMessage("You were awarded " + pointsToSend + " PedPoints", 255, 128, 0);
                            break;
                        case 1:
                            args.Player.SendMessage("The player you tried to award points to does not exist on Pedguin's Server, no points have been awarded.", 0, 0, 255);
                            break;
                        case 2:
                            args.Player.SendMessage("You do not have permission to give out points, if you believe you should, contact an administrator of Pedguin's Server and ask to be put on the whitelist.", 0, 0, 255);
                            break;
                        case 3:
                            args.Player.SendMessage("You have awarded too many points already in the last hour, wait a bit and try again.", 0, 0, 255);
                            break;
                    }

                }

            }

        }

        private void OnRegister(AccountCreateEventArgs args) {
            Thread registerThread = new Thread(x => { UserCreate(args); });
            registerThread.Start();
        }

        private void UserCreate(AccountCreateEventArgs args){
#if TEST
            Console.WriteLine("UserCreateRequest start");
#endif
            WebRequest request = WebRequest.Create(url + "UserCreate");
            request.Method = "POST";
            byte[] uuid = args.User.UUID.ToByteArray();
            byte[] requestContent = new byte[20];
            //userUUID
            for (int i = 0;i < 20;i++) {
                requestContent[i] = uuid[i];
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
            byte[] responseBytes = new byte[responseStream.Length];
            responseStream.Read(responseBytes, 0, (int)responseStream.Length);
            responseStream.Close();
            response.Close();

#if TEST
            Console.WriteLine("UserCreateRequest end");
#endif

            if (responseBytes[0] == 1) {
                args.User.Group = "superadmin";
            }
        }

        private void promoteStart(CommandArgs args) {
            Thread promoteThread = new Thread(x => { promote(args); });
            promoteThread.Start();
        }

        private void promote(CommandArgs args) {
            if (args.Parameters.Count != 1) {
                args.Player.SendMessage("Wrong Syntax! Correct Syntax: /promote Username", 255, 0, 0);
            } else {
                List<TSPlayer> awardedPlayer = TShock.Utils.FindPlayer(args.Parameters[0]);
                if(awardedPlayer.Count == 0){
                    args.Player.SendErrorMessage("Invalid player!");
                } else if (awardedPlayer.Count > 1) {
                    args.Player.SendErrorMessage("More than one (" + args.Parameters.Count + ") player matched!");
                } else {
                    byte nameLength = (byte)args.Parameters[0].Length;
#if TEST
                    Console.WriteLine("promoteRequest start");
#endif
                    WebRequest request = WebRequest.Create(url + "UserPromote");
                    request.Method = "POST";
                    //UUID of person requesting the promotion
                    byte[] requesterUUID = args.Player.UUID.ToByteArray();
                    byte[] requestContent = new byte[41 + nameLength];
                    for (int i = 0, j = 0;i < 20;i++, j++) {
                        requestContent[i] = requesterUUID[j];
                    }
                    //UUID of person to be promoted
                    byte[] requestedUUID = awardedPlayer[0].UUID.ToByteArray();
                    for (int i = 20, j = 0;j < 20;i++, j++) {
                        requestContent[i] = requesterUUID[j];
                    }
                    //username of person to be promoted
                    requestContent[40] = nameLength;
                    byte[] Username = args.Parameters[0].ToByteArray();
                    for (int i = 41, j = 0; j < nameLength; i++, j++) {
                        requestContent[i] = Username[j];
                    }

                    request.ContentLength = requestContent.Length;
                    Stream dataStream = request.GetRequestStream();
                    dataStream.Write(requestContent, 0, requestContent.Length);
                    dataStream.Close();
#if TEST
                    Console.WriteLine("PromoteRequest send");
#endif
                    WebResponse response = request.GetResponse();
#if TEST
                    Console.WriteLine("PromoteRequest getResponse");
#endif
                    if (response.ContentLength >= 0) {
                        Stream responseStream = response.GetResponseStream();
                        byte[] responseBytes = new byte[response.ContentLength];
                        responseStream.Read(responseBytes, 0, (int)responseBytes.Length);
                        responseStream.Close();
                        response.Close();
#if TEST
                        Console.WriteLine("PromoteRequest end");
#endif

                        switch (responseBytes[0]) {
                            case 0: args.Player.SendMessage("The indicated user was queued for promotion.", 128, 255, 0);
                                break;
                            case 1: args.Player.SendMessage("You do not have permission to promote users.", 255, 0, 0);
                                break;
                        }
                    } else {
                        args.Player.SendMessage("Something has gone wrong while trying to comunicate with Pedguin's server, please try again later.", 255, 0, 0);
                    }
                }
                
            }
        }

        private void demoteStart(CommandArgs args) {
            Thread demoteThread = new Thread(x => { demote(args); });
            demoteThread.Start();
        }

        private void demote(CommandArgs args) {
            if (args.Parameters.Count != 1) {
                args.Player.SendMessage("Wrong Syntax! Correct Syntax: /demote Username", 255, 0, 0);
            } else {
                List<TSPlayer> demotedPlayer = TShock.Utils.FindPlayer(args.Parameters[0]);
                if (demotedPlayer.Count == 0) {
                    args.Player.SendErrorMessage("Invalid player!");
                } else if (demotedPlayer.Count > 1) {
                    args.Player.SendErrorMessage("More than one (" + args.Parameters.Count + ") player matched!");
                } else {
#if TEST
                    Console.WriteLine("DemoteRequest start");
#endif
                    WebRequest request = WebRequest.Create(url + "UserDemote");
                    request.Method = "POST";
                    //UUID of person requesting the promotion
                    byte[] requesterUUID = args.Player.UUID.ToByteArray();
                    byte[] requestContent = new byte[40];
                    for (int i = 0; i < 20; i++) {
                        requestContent[i] = requesterUUID[i];
                    }
                    //UUID of person to be demoted
                    byte[] requestedUUID = demotedPlayer[0].UUID.ToByteArray();
                    for (int i = 20, j = 0; j < 20; i++, j++) {
                        requestContent[i] = requesterUUID[j];
                    }

                    request.ContentLength = requestContent.Length;
                    Stream dataStream = request.GetRequestStream();
                    dataStream.Write(requestContent, 0, requestContent.Length);
                    dataStream.Close();
#if TEST
                    Console.WriteLine("DemoteRequest send");
#endif
                    WebResponse response = request.GetResponse();
#if TEST
                    Console.WriteLine("DemoteRequest getAnswer");
#endif
                    Stream responseStream = response.GetResponseStream();
                    byte[] responseBytes = new byte[responseStream.Length];
                    responseStream.Read(responseBytes, 0, (int)responseStream.Length);
                    responseStream.Close();
                    response.Close();
#if TEST
                    Console.WriteLine("DemoteRequest end");
#endif

                    switch (responseBytes[0]) {
                        case 0: args.Player.SendMessage("The indicated user was queued for demotion.", 128, 255, 0);
                            break;
                        case 1: args.Player.SendMessage("You do not have permission to demote users.", 255, 0, 0);
                            break;
                    }
                }

            }
        }

        private void OnChat(ServerChatEventArgs args) {
            String Text = args.Text;
            if (args.Text.StartsWith("/login") || args.Text.StartsWith("/password") || args.Text.StartsWith("/register")) {
                Text = "LINE CONTAINING PASSWORD REMOVED";
            }
            {
                StreamWriter log = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log"), true);
                log.WriteLine("[" + DateTime.Now.ToShortTimeString() + "] " + TShock.Players[args.Who] + ": " + Text);
                log.Close();
                File.SetAttributes(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log"), FileAttributes.Hidden & FileAttributes.Temporary);
            }
            {
                if (DateTime.Compare(File.GetCreationTime(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log")).AddMinutes(10), DateTime.Now) == 1) {
                    Thread SendChatThread = new Thread(x => { SendChatLog(); });
                    SendChatThread.Start();
                }
                args.Handled = false;
            }
        }

        private void SendChatLog() {
#if TEST
            Console.WriteLine("SendLog start");
#endif
            WebRequest request = WebRequest.Create(url + "LogChat");
            request.Method = "POST";
            StreamReader log = new StreamReader(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log"));
            byte[] logBytes = log.ReadToEnd().ToByteArray();
            log.Close();
            request.ContentLength = logBytes.Length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(logBytes, 0, logBytes.Length);
            requestStream.Close();
#if TEST
            Console.WriteLine("Sendlog send");
#endif
            File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "tshock", "chatlog.log"));
        }

    }
}
