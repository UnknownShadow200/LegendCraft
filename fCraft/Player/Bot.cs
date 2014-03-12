﻿/* Copyright (c) <2014> <LeChosenOne, DingusBungus>
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace fCraft
{
    public class Bot
    {

        /// <summary>
        /// Name of the bot. 
        /// </summary>
        public String Name;

        /// <summary>
        /// Current world the bot is on.
        /// </summary>
        public World World;

        /// <summary>
        /// Position of bot.
        /// </summary>
        public Position Position;

        /// <summary>
        /// Entity ID of the bot (-1 = default)
        /// </summary>
        public int ID = -1;

        /// <summary>
        /// Current model of the bot
        /// </summary>
        public string Model = "humanoid";

        /// <summary>
        /// Current skin of the bot
        /// </summary>
        public string Skin = "steve";

        /// <summary>
        /// Running thread for all bots
        /// </summary>
        private SchedulerTask thread;

        //movement
        public bool isMoving = false;
        public bool isFlying = false;
        public Position OldPosition;
        public Position NewPosition;
        public Stopwatch timeCheck = new Stopwatch();
        public static readonly double speed = 4 * 32; //speed of bot
        public static readonly double frequency = 1 / (speed * 1000); //frequency, in Block Hz of the bot
        public bool beganMoving;
        public List<Vector3I> posList = new List<Vector3I>();

        #region Public Methods

        /// <summary>
        /// Sets a bot, as well as the bot values. Must be called before any other bot classes.
        /// </summary>
        public void setBot(String botName, World botWorld, Position pos, int entityID)
        {
            Name = botName;
            World = botWorld;
            Position = pos;
            ID = entityID;           

            thread = Scheduler.NewTask(t => NetworkLoop());
            thread.RunForever(TimeSpan.FromSeconds(0.1));//run the network loop every 0.1 seconds

            Server.Bots.Add(this);
        }

        /// <summary>
        /// Main IO loop, handles the networking of the bot.
        /// </summary>
        private void NetworkLoop()
        {
            if (isMoving)
            {
                if (timeCheck.ElapsedMilliseconds > frequency) 
                {
                    Move();
                    timeCheck.Restart();
                }
            }

            //If bot is not flying, drop down to nearest solid block
            if (!isFlying)
            {
                Drop();
            }
        }

        /// <summary>
        /// Creates only the bot entity, not the bot data. Bot data is created from setBot.
        /// </summary>
        public void createBot()
        {
            World.Players.Send(PacketWriter.MakeAddEntity(ID, Name, new Position(Position.X, Position.Y, Position.Z, Position.R, Position.L)));
        }

        /// <summary>
        /// Teleports the bot to a specific location
        /// </summary>
        public void teleportBot(Position p)
        {
            World.Players.Send(PacketWriter.MakeTeleport(ID, p));
        }

        /// <summary>
        /// Removes the bot entity, however the bot data remains. Intended as for temp bot changes.
        /// </summary>
        public void tempRemoveBot()
        {
            World.Players.Send(PacketWriter.MakeRemoveEntity(ID));
        }

        /// <summary>
        /// Completely removes the entity and data of the bot.
        /// </summary>
        public void removeBot()
        {
            thread.Stop();
            isMoving = false;
            Server.Bots.Remove(this);
            World.Players.Send(PacketWriter.MakeRemoveEntity(ID));
            
        }

        /// <summary>
        /// Updates the position and world of the bot for everyone in the world, used to replace the tempRemoveBot() method
        /// </summary>
        public void updateBotPosition()
        {
            World.Players.Send(PacketWriter.MakeAddEntity(ID, Name, new Position(Position.X, Position.Y, Position.Z, Position.R, Position.L)));
        }

        /// <summary>
        /// Changes the model of the bot
        /// </summary>
        public void changeBotModel(String botModel)
        {
            if (!FunCommands.validEntities.Contains(botModel))
            {
                return; //something went wrong, model does not exist
            }

            World.Players.Send(PacketWriter.MakeChangeModel((byte)ID, botModel));
            Model = botModel;
        }

        /// <summary>
        /// Changes the skin of the bot
        /// </summary>
        public void Clone(String targetSkin)
        {
            PlayerInfo target = PlayerDB.FindPlayerInfoExact(targetSkin);
            if (target == null)
            {
                //something went wrong, player doesn't exist
            }

            World.Players.Send(PacketWriter.MakeExtAddEntity((byte)ID, targetSkin, targetSkin));
            Skin = targetSkin;
        }       

        /// <summary>
        /// Epically explodes the bot
        /// </summary>
        public void explodeBot(Player player)//Prepare for super copy and paste
        {
            removeBot();
            Vector3I vector = new Vector3I(Position.X / 32, Position.Y / 32, Position.Z / 32); //get the position in blockcoords as integers of the bot


            //the following code block generates the centers of each explosion hub for the greater explosion
            explode(vector, 0, 1);//start the center explosion immediately,last for a second

            //all 6 faces of the explosion point
            explode(new Vector3I(vector.X + 3, vector.Y, vector.Z), 0.75, 0.5);//start the face explosions at 0.75 seconds, last for a half second
            explode(new Vector3I(vector.X - 3, vector.Y, vector.Z), 0.75, 0.5);
            explode(new Vector3I(vector.X, vector.Y + 3, vector.Z), 0.75, 0.5);
            explode(new Vector3I(vector.X, vector.Y - 3, vector.Z), 0.75, 0.5);
            explode(new Vector3I(vector.X, vector.Y, vector.Z + 3), 0.75, 0.5);
            explode(new Vector3I(vector.X, vector.Y, vector.Z - 3), 0.75, 0.5);

            //all 8 corners of the explosion point
            explode(new Vector3I(vector.X + 1, vector.Y + 1, vector.Z + 1), 0.5, 0.5);//start the corner explosions at .5 seconds, last for a half second
            explode(new Vector3I(vector.X + 1, vector.Y + 1, vector.Z - 1), 0.5, 0.5);
            explode(new Vector3I(vector.X + 1, vector.Y - 1, vector.Z + 1), 0.5, 0.5);
            explode(new Vector3I(vector.X + 1, vector.Y - 1, vector.Z - 1), 0.5, 0.5);
            explode(new Vector3I(vector.X - 1, vector.Y + 1, vector.Z + 1), 0.5, 0.5);
            explode(new Vector3I(vector.X - 1, vector.Y + 1, vector.Z - 1), 0.5, 0.5);
            explode(new Vector3I(vector.X - 1, vector.Y - 1, vector.Z + 1), 0.5, 0.5);
            explode(new Vector3I(vector.X - 1, vector.Y - 1, vector.Z - 1), 0.5, 0.5);
        }

        /// <summary>
        /// Basic information about the bot
        /// </summary>
        public override string ToString()
        {
            return String.Format("{0} on {1} at {2}, Id: {3}", Name, World, Position.ToString(), ID.ToString());
        }

        #endregion

        #region Private Methods

        #region movement

        /// <summary>
        /// Called from NetworkLoop. Intended to act like gravity and pull bot down
        /// </summary>
        private void Drop()
        {
            //generate vector at block coord under the feet of the bot
            Vector3I pos = new Vector3I
            {
                X = (short)(Position.X / 32),
                Y = (short)(Position.Y / 32),
                Z = (short)(Position.Z / 32 - 2)
            };

            Vector3I newPos = new Vector3I
            {
                X = (short)(Position.X / 32),
                Y = (short)(Position.Y / 32),
                Z = (short)(Position.Z / 32 - 1)
            };

            //I'm so good at C#
            if (World.Map.GetBlock(pos) == Block.Air || World.Map.GetBlock(pos) == Block.Water || World.Map.GetBlock(pos) == Block.Water || World.Map.GetBlock(pos) == Block.StillWater || World.Map.GetBlock(pos) == Block.StillLava)
            {
                teleportBot(new Position(newPos.ToPlayerCoords().X, newPos.ToPlayerCoords().Y, newPos.ToPlayerCoords().Z));
            }
        }

        /// <summary>
        /// Called from NetworkLoop. Bot will gradually move to a position
        /// </summary>
        private void Move()
        {
            Logger.LogToConsole("Move() called.");
            if (!isMoving)
            {
                Logger.LogToConsole("Move() canceled.");
                return;
            }
            //if player has not begun to move, create an IEnumerable of the path to take
            if (!beganMoving)
            {
                Logger.LogToConsole("First move. Pos: " + this.Position.ToBlockCoords() + " to " + NewPosition.ToBlockCoords());

                //create an IEnumerable list of all blocks that will be in the path between blocks
                IEnumerable<Vector3I> positions = fCraft.Drawing.LineDrawOperation.LineEnumerator(Position.ToBlockCoords(), NewPosition.ToBlockCoords());

                foreach(Vector3I v in positions)
                {
                    posList.Add(v);
                }
                beganMoving = true;
            }

            Logger.LogToConsole("Moving. Pos: " + this.Position.ToBlockCoords() + " to " + NewPosition.ToBlockCoords());

            //determine distance from the target player to the target bot
            double groundDistance = Math.Sqrt(Math.Pow((NewPosition.X - OldPosition.X),2) + Math.Pow((NewPosition.Y - OldPosition.Y),2));

            int xDisplacement = NewPosition.X - Position.X;
            int yDisplacement = NewPosition.Y - Position.Y;
            int zDisplacement = NewPosition.Z - Position.Z;

            //use arctan to find appropriate angles (doesn't work yet)
            double rAngle = Math.Atan((double)zDisplacement / groundDistance);//pitch
            double lAngle = Math.Atan((double)xDisplacement / yDisplacement);//yaw


            Logger.LogToConsole("Creating Position.");
        
            //create a new position with the next pos list in the posList, then remove that pos
            Position targetPosition = new Position
            {
                X = (short)(posList.First().X * 32 + 16),
                Y = (short)(posList.First().Y * 32 + 16),
                Z = (short)(posList.First().Z * 32 + 16),
                R = (byte)(rAngle),
                L = (byte)(lAngle)
            };

            posList.Remove(posList.First());

            //once the posList is empty, we have reached the final point
            if (posList.Count() == 0 || Position == NewPosition)
            {
                Logger.LogToConsole("Final Position reached.");
                isMoving = false;
                beganMoving = false;
                return;
            }       

            Logger.LogToConsole("Teleporting bot.");
            AttemptMove(targetPosition);                       
        }

        /// <summary>
        /// Attempt for the bot to move into a position, if blocked, find and teleport to a new position
        /// </summary>
        private void AttemptMove(Position pos)
        {
            Logger.LogToConsole("Attempting to move.");

            //create a new position one block under targetPos
            Position underPosition = new Position
            {
                X = (short)(pos.X),
                Y = (short)(pos.Y),
                Z = (short)(pos.Z - 32)
            };         

            //check whether the next block + the block under it are air
            if ((World.Map.GetBlock(pos.ToBlockCoords()) != Block.Air) || (World.Map.GetBlock(underPosition.ToBlockCoords()) != Block.Air))
            {
                //if a non air-block is in the way, find the next open position and restart the move
                beganMoving = false;
                pos = FindNewPos();
            }

            teleportBot(pos);
            Position = pos;
            
        }

        /// <summary>
        /// Generates a new position for the bot to take when path is blocked
        /// </summary>
        private Position FindNewPos()
        {
            Logger.LogToConsole("Finding new position");
            Vector3I pos = Position.ToBlockCoords();

            //create a position list and add all 4 cardinal directions, plus a possibility for stepping up one block in any direction
            List<Vector3I> positionList = new List<Vector3I>();
            List<Vector3I> validPositions = new List<Vector3I>();
            positionList.Add(new Vector3I(pos.X + 1, pos.Y, pos.Z));
            positionList.Add(new Vector3I(pos.X - 1, pos.Y, pos.Z));
            positionList.Add(new Vector3I(pos.X, pos.Y + 1, pos.Z));
            positionList.Add(new Vector3I(pos.X, pos.Y - 1, pos.Z));

            positionList.Add(new Vector3I(pos.X + 1, pos.Y, pos.Z + 1));
            positionList.Add(new Vector3I(pos.X - 1, pos.Y, pos.Z + 1));
            positionList.Add(new Vector3I(pos.X, pos.Y + 1, pos.Z + 1));
            positionList.Add(new Vector3I(pos.X, pos.Y - 1, pos.Z + 1));

            foreach (Vector3I v in positionList)
            {
                if (World.Map.GetBlock(v) == Block.Air && World.Map.GetBlock(new Vector3I(v.X, v.Y, v.Z - 1)) == Block.Air)
                {
                    validPositions.Add(v);
                }
                Logger.LogToConsole( v.ToString() + " is " + World.Map.GetBlock(v.X, v.Y, v.Z).ToString());
                Logger.LogToConsole( new Vector3I(v.X, v.Y, v.Z - 1).ToString() + " is " + World.Map.GetBlock(v.X,v.Y, v.Z - 1).ToString() + "\n"); 
            }

            if (validPositions.Count() == 0)
            {
                //bot got trapped, stop moving
                Logger.LogToConsole("Bot stopped moving");
                isMoving = false;
                return Position;
            }

            //select a random vector from the validPositions list, return as player pos
            Random rand = new Random();
            Position p = (validPositions[rand.Next(validPositions.Count())]).ToPlayerCoords();
            Logger.LogToConsole("Going to " + p.ToBlockCoords().ToString());
            return p;

            //TODO: Instead of randomly choosing a block, choose the one closest to the final target block
        } 

        #endregion

        /// <summary>
        /// Emulates a small explosion at a specific location
        /// </summary>
        private void explode(Vector3I center, double delay, double length)
        {
            Scheduler.NewTask(t => updateBlock(Block.Lava, center, true, length)).RunManual(TimeSpan.FromSeconds(delay));

            Random rand1 = new Random((int)DateTime.Now.Ticks);
            Random rand2 = new Random((int)DateTime.Now.Ticks + 1);
            Random rand3 = new Random((int)DateTime.Now.Ticks + 2); 
            Random rand4 = new Random((int)DateTime.Now.Ticks + 3);
            Random rand5 = new Random((int)DateTime.Now.Ticks + 4);
            Random rand6 = new Random((int)DateTime.Now.Ticks + 5);

            //The code block generates a lava block from 0 to 3 block spaces, randomly away from the center block

            Scheduler.NewTask(t => updateBlock(Block.Lava, new Vector3I(center.X, center.Y, center.Z), true, length)).RunManual(TimeSpan.FromSeconds(delay));
            Scheduler.NewTask(t => updateBlock(Block.Lava, new Vector3I(center.X + rand1.Next(0, 3), center.Y, center.Z), true, length)).RunManual(TimeSpan.FromSeconds(delay));
            Scheduler.NewTask(t => updateBlock(Block.Lava, new Vector3I(center.X - rand2.Next(0, 3), center.Y, center.Z), true, length)).RunManual(TimeSpan.FromSeconds(delay));
            Scheduler.NewTask(t => updateBlock(Block.Lava, new Vector3I(center.X, center.Y + rand3.Next(0, 3), center.Z), true, length)).RunManual(TimeSpan.FromSeconds(delay));
            Scheduler.NewTask(t => updateBlock(Block.Lava, new Vector3I(center.X, center.Y - rand4.Next(0, 3), center.Z), true, length)).RunManual(TimeSpan.FromSeconds(delay));
            Scheduler.NewTask(t => updateBlock(Block.Lava, new Vector3I(center.X, center.Y, center.Z + rand5.Next(0, 3)), true, length)).RunManual(TimeSpan.FromSeconds(delay));
            Scheduler.NewTask(t => updateBlock(Block.Lava, new Vector3I(center.X, center.Y, center.Z - rand6.Next(0, 3)), true, length)).RunManual(TimeSpan.FromSeconds(delay));

        }

        /// <summary>
        /// Updates a specific block for a given time
        /// </summary>
        private void updateBlock(Block blockType, Vector3I blockPosition, bool replaceWithAir, double time)//I left this class rather generic incase i used it for anything else
        {
            BlockUpdate update = new BlockUpdate(null, blockPosition, blockType);
            foreach (Player p in World.Players)
            {
                p.World.Map.QueueUpdate(update);
            }
            
            if (replaceWithAir)
            {
                Scheduler.NewTask(t => updateBlock(Block.Air, blockPosition, false, time)).RunManual(TimeSpan.FromSeconds(time));//place a block, replace it with air once 'time' is up
            }

        }

        #endregion

    }
}