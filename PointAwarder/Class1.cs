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
            }
            base.Dispose(disposing);
        }

        public override void Initialize() {
            Commands.ChatCommands.Add(new Command("pedguinServer.award", awardStart, "award"));
            AccountHooks.AccountCreate += OnRegister;
            //TODO: chat logs
            //TODO: add a command that takes a mod's UUID and puts it on a list of UUIDs to approve @ home
            //TODO: add a command that removes a mod from the UUID list @ home (reversible or to be confirmed)
        }

        private void awardStart(CommandArgs args) {
            Thread awardThread = new Thread(x => { award(args); });
            awardThread.Start();
        }

        private void award(CommandArgs args) {

            int pointsToSend;

            if (args.Parameters.Count != 2) {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /award PlayerToAward PointToAward", 255, 0, 0);
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

                    WebRequest request = WebRequest.Create("http://www.pedguin.com/remoteAward");
                    request.Method = "POST";
                    byte[] awarderUUIDbytes = awarderUUID.ToByteArray();
                    byte[] awardedUUIDbytes = awardedUUID.ToByteArray();
                    byte[] byteArray = new byte[18];
                    //first 8 chars of awarder UUID
                    byteArray[0] = awarderUUIDbytes[0];
                    byteArray[1] = awarderUUIDbytes[1];
                    byteArray[2] = awarderUUIDbytes[2];
                    byteArray[3] = awarderUUIDbytes[3];
                    byteArray[4] = awarderUUIDbytes[4];
                    byteArray[5] = awarderUUIDbytes[5];
                    byteArray[6] = awarderUUIDbytes[6];
                    byteArray[7] = awarderUUIDbytes[7];
                    //first 8 chars of awarded UUID
                    byteArray[8] = awardedUUIDbytes[0];
                    byteArray[9] = awardedUUIDbytes[1];
                    byteArray[10] = awardedUUIDbytes[2];
                    byteArray[11] = awardedUUIDbytes[3];
                    byteArray[12] = awardedUUIDbytes[4];
                    byteArray[13] = awardedUUIDbytes[5];
                    byteArray[14] = awardedUUIDbytes[6];
                    byteArray[15] = awardedUUIDbytes[7];
                    //amount of points
                    byteArray[16] = (byte)pointsToSend;
                    //number of players on server
                    byteArray[17] = (byte)TShock.Utils.ActivePlayers();

                    request.ContentType = "byteStream";    //TODO: this has to be filled out with a valid content type
                    request.ContentLength = byteArray.Length;
                    Stream dataStream = request.GetRequestStream();
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    dataStream.Close();
                    WebResponse response = request.GetResponse();
                    Stream responseStream = response.GetResponseStream();
                    byte[] responseBytes = new byte[responseStream.Length];
                    responseStream.Read(responseBytes, 0, (int)responseStream.Length);
                    responseStream.Close();
                    response.Close();

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
            WebRequest request = WebRequest.Create("http://www.pedguin.com/UserCreate");    //TODO: answer this on the other side
            request.Method = "POST";
            byte[] uuid = args.User.UUID.ToByteArray();
            byte[] requestContent = new byte[8];
            //userUUID
            requestContent[0] = uuid[0];
            requestContent[1] = uuid[1];
            requestContent[2] = uuid[2];
            requestContent[3] = uuid[3];
            requestContent[4] = uuid[4];
            requestContent[5] = uuid[5];
            requestContent[6] = uuid[6];
            requestContent[7] = uuid[7];

            request.ContentType = "byteStream"; //TODO: fill this out with a valid value
            request.ContentLength = requestContent.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(requestContent, 0, requestContent.Length);
            dataStream.Close();
            WebResponse response = request.GetResponse();
            Stream responseStream = response.GetResponseStream();
            byte[] responseBytes = new byte[responseStream.Length];
            responseStream.Read(responseBytes, 0, (int)responseStream.Length);
            responseStream.Close();
            response.Close();

            if (responseBytes[0] == 1) {
                args.User.Group = "superadmin";
            }
        }
    }
}
