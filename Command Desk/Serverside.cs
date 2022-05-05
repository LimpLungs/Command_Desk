using System;
using System.Runtime;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace Command_Desk
{
    internal class Serverside
    {
        public static void StartServer()
        {
            Console.WriteLine("Starting server...");

            var SERVER_IP_ADDRESS = IPAddress.Parse(Program.SERVER_IP_STRING);
            var SERVER_IP_ENDPOINT = new IPEndPoint(SERVER_IP_ADDRESS, Program.PORT_INT);

            var SERVER_SOCKET = new Socket(SERVER_IP_ADDRESS.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                SERVER_SOCKET.Bind(SERVER_IP_ENDPOINT);
                SERVER_SOCKET.Listen(1);
                Console.WriteLine("Bound and listening..." + SERVER_IP_ADDRESS.ToString());
            }
            catch (SocketException e) { Console.WriteLine(e); }

            if (SERVER_IP_ADDRESS != null && SERVER_IP_ENDPOINT != null)
            {
                if (SERVER_SOCKET != null)
                {
                    var loop = true;

                    while (loop)
                    {
                        // Limit active threads to MAX_CLIENTS to avoid out of memory exceptions.
                        // This is essentially the maximum number of active clients on the server, techs or users alike.
                        // I don't remember having this issue while doing the class assignments, but this is a good enough workaround for this product.
                        if (Program.ACTIVE < Program.MAX_CLIENTS)
                            try
                            {
                                Program.ACTIVE++;
                                var THREAD = new Thread(new ThreadStart(() => ServerClientHandler(SERVER_SOCKET.Accept())));
                                THREAD.Start();
                            }
                            catch (SocketException e)
                            {
                                Console.WriteLine(e);
                            }
                    }
                }
            }
        }

        private static void ServerClientHandler(Socket handler)
        {
            Console.WriteLine("Client detected on {0}, waiting for authentication handshake.", handler.RemoteEndPoint);

            handler.Send(Program.Command(Program.COMMAND_SERVER_TO_CLIENT_HELLO));

            Client responder = new Client(ClientType.NONE, ClientType.NONE);


            var THREAD_TIMER = 0;
            var THREAD_TIMER_MAX = 100000;
            var stage = 0;
            var username = "";

            while (stage >= 0)
            {
                try
                {
                    Packet response = new Packet(handler);

                    responder = new Client(response.message);

                    var increase = false;

                    if (response.size > 0)
                    {
                        if (stage == 0 && response.message.Substring(0, response.message.IndexOf('@')) == "RETURNCLIENTTYPE")
                        {
                            Console.WriteLine("0-- " + response.message);

                            var type = responder.sender;

                            Console.WriteLine(type);

                            switch (type)
                            {
                                case ClientType.USER:
                                    handler.Send(Program.Command(Program.COMMAND_SERVER_TO_USER_HELLO));
                                    increase = true;
                                    break;
                                case ClientType.TECH:
                                    handler.Send(Program.Command(Program.COMMAND_SERVER_TO_TECH_HELLO));
                                    increase = true;
                                    break;
                                default:
                                    stage = -1;
                                    break;
                            }
                        }

                        if (stage == 1)
                        {
                            username = response.message.Substring(0, response.message.IndexOf('~'));
                            var password = response.message.Substring(response.message.IndexOf('~') + 1, response.message.IndexOf('@') - response.message.IndexOf('~') - 1);
                            var type = responder.sender;

                            Console.WriteLine("{0}, {1} connected.", handler.RemoteEndPoint, username);

                            var valid = validUserCredentials(username, password, responder.sender.ToString());

                            Console.WriteLine(valid);

                            handler.Send(Program.Command("LOGIN~" + valid.ToString().ToUpper() + "@" + type + Program.COMMAND_APPEND_SENDER));

                            if (valid)
                                increase = true;
                            else
                                stage = 0;
                        }

                        if (response.message == string.Format(Program.COMMAND_GOING_MENU, responder.target_append, responder.sender_append))
                        {
                            Console.WriteLine(string.Format("Main Menu Request from {0}", username));

                            handler.Send(Program.Command(string.Format(Program.COMMAND_OK, "@" + responder.sender, "[" + responder.target.ToString().ElementAt(0) + "]")));
                        }

                        if (response.message.Split('~')[0] == Program.COMMAND_GOING_NEW_TICKET && response.message.Split('~').Length == 8 && responder.target == ClientType.SERVER)
                        {
                            Console.WriteLine(string.Format("New Ticket Action Request from {0}", username));

                            var RequesterClientType = response.message.Split('~')[1];
                            var RequesterUserName = response.message.Split('~')[2];
                            var IssueDescription = response.message.Split('~')[3];
                            var TechnicianClientType = ClientType.TECH.ToString();
                            var TechnicianUserName = leastBusyTechnicianAssign();
                            var TechnicianResponse = "Waiting on response.";

                            string ticket = String.Concat(RequesterClientType, "~", RequesterUserName.ToUpper(), "~", IssueDescription, "~", TechnicianClientType, "~", TechnicianUserName, "~", TechnicianResponse, "|OPEN|\n");

                            File.AppendAllText("C:\\CommandDesk_Tickets.txt", ticket);

                            handler.Send(Program.Command(string.Format(Program.COMMAND_OK, "@" + responder.sender, "[" + responder.target.ToString().ElementAt(0) + "]")));
                        }
                        
                        if (response.message == string.Format(Program.COMMAND_GOING_VIEW_TICKET, responder.target_append, responder.sender_append))
                        {
                            Console.WriteLine(string.Format("View Ticket Menu Request from {0}", username));

                            handler.Send(Program.Command(string.Format(Program.COMMAND_TICKET_LIST, "@" + responder.sender, "[" + responder.target.ToString().ElementAt(0) + "]", getTicketList(username, responder.sender))));
                        }
                        
                        if (response.message == string.Format(Program.COMMAND_GOING_CLOSE_TICKET, responder.target_append, responder.sender_append))
                        {
                            Console.WriteLine(string.Format("Close Ticket Menu Request from {0}", username));

                            handler.Send(Program.Command(string.Format(Program.COMMAND_TICKET_LIST, "@" + responder.sender, "[" + responder.target.ToString().ElementAt(0) + "]", getTicketList(username, responder.sender))));
                        }

                        if (response.message.Split('~')[0] == Program.COMMAND_EDIT_TICKET.Substring(0, 6) && response.message.Split('~').Length == 7 && responder.target == ClientType.SERVER)
                        {
                            Console.WriteLine(String.Format("Edit Ticket Action Request from {0}", username));

                            var RequesterClientType = response.message.Split('~')[1];
                            var RequesterUserName = response.message.Split('~')[2];
                            var IssueDescription = response.message.Split('~')[3];
                            var TechnicianClientType = response.message.Split('~')[4];
                            var TechnicianUserName = response.message.Split('~')[5];
                            var TechnicianResponse = response.message.Split('~')[6];
                            var TechnicianStatus = response.message.Split('~')[7];
                            
                            string ticket_line = String.Concat(RequesterClientType, "~", RequesterUserName.ToUpper(), "~", IssueDescription, "~", TechnicianClientType, "~", TechnicianUserName, "~", TechnicianResponse, "|" + TechnicianStatus + "|", "\n");
                            
                            Ticket ticket = new Ticket(ticket_line);

                            editLine(ticket);
                        }

                        if (increase)
                            stage++;
                    }
                }
                catch (SocketException se) { }


                // Timeout on no response
                //
                // (will depend on system speed since it's using iterations through the loop and determined by big O's time to complete.
                // Slower servers will mean the timeout takes longer to happen.
                // Would want to change this out in the future to use system time calculation)
                if (THREAD_TIMER++ > THREAD_TIMER_MAX)
                {
                    stage = -1;
                }
            }

            Console.WriteLine(responder.target.ToString() + "  " + responder.sender.ToString());
            Console.WriteLine("Connection closed on client {0}", handler.RemoteEndPoint);
            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
            Program.ACTIVE--;

        }


        //
        // Summary:
        //     Finds a user in the command desk credentials file, or creates one if they don't exist.
        //
        // Parameters:
        //     username: string username
        //     password_hashcode: the hashcode of the password string should be passed in.
        //     client_type: the sender's ClientType.ToString() should be passed in.
        private static bool validUserCredentials(String username, String password_hashcode, String client_type)
        {
            bool valid = false;


            // Entire implementation can probably change to a method like File.AppendText which handles creation when 
            // file does not exist and handles closing, etc. No need to manually call everything in the future.
            if (!File.Exists("C:\\CommandDesk_Credentials.txt"))
                File.WriteAllText("C:\\CommandDesk_Credentials.txt", Program.MASTER_USER + '~' + Program.MASTER_PASS.GetHashCode() + "~" + ClientType.TECH.ToString() + "\n");
                
            

            var data = File.ReadAllLines("C:\\CommandDesk_Credentials.txt");
            var ndata = data;

            int index = 0;
            bool found = false; 
            
            foreach (var row in data)
            {
                if (row.Split('~')[0] == username)
                {
                    Console.WriteLine(row.Split('~')[0] + " matches " + username);

                    found = true;

                    if (found)
                    {
                        if (row.Split('~')[1] == password_hashcode && row.Split('~')[2] == client_type)
                        {
                            Console.WriteLine("Authentication successful for " + username);
                            valid = true;
                        }
                        else
                            Console.WriteLine("Bad credentials received.");
                    }
                }

                index += 1;
            }

            if (!found)
            {
                Array.Resize(ref ndata, ndata.Length + 1);
                ndata[ndata.Length - 1] = username.ToUpper() + '~' + password_hashcode + "~" + client_type + "~";
                valid = true;
            }

            File.Delete("C:\\CommandDesk_Credentials.txt");
            File.WriteAllLines("C:\\CommandDesk_Credentials.txt", ndata);

            return valid;
        }

        private static string leastBusyTechnicianAssign()
        {

            if (!File.Exists("C:\\CommandDesk_Credentials.txt"))
                File.Create("C:\\CommandDesk_Credentials.txt").Close();

            if (!File.Exists("C:\\CommandDesk_Tickets.txt"))
                File.Create("C:\\CommandDesk_Tickets.txt").Close();

            var tech_data = File.ReadAllLines("C:\\CommandDesk_Credentials.txt");
            var ticket_data = File.ReadAllLines("C:\\CommandDesk_Tickets.txt");
            var techs = new List<string>();

            for (int i = 0; i < ticket_data.Length; i++)
                ticket_data[i] = ticket_data[i].Split('~')[4];

            Array.Sort(ticket_data);
            Array.Sort(tech_data);

            foreach (var tech in tech_data)
                if (tech.Split('~')[2] == ClientType.TECH.ToString())
                {
                    techs.Add(tech.Split('~')[0]);
                    Console.WriteLine(techs.ElementAt(techs.Count - 1));
                }

            string username = Program.MASTER_USER;
            int count = 0;
            int min = Int32.MaxValue;

            foreach (var tech in techs)
            {
                foreach (var ticket in ticket_data)
                {
                    if (ticket.ToUpper() == tech.ToUpper())
                    {
                        count += 1;
                    }
                }

                if (count < min)
                {
                    if ((techs.Count > 1 && tech.ToUpper() != Program.MASTER_USER.ToUpper()) || techs.Count == 1)
                    {
                        min = count;
                        username = tech.ToUpper();
                    }
                    else
                        Console.WriteLine("No TECHS found. Was Tech file deleted during write?");
                }

                count = 0;
            }

            return username;
        }

        private static string getTicketList(string username, ClientType type)
        {
            if (File.Exists("C:\\CommandDesk_Tickets.txt"))
            {
                int count = 0;
                string ticket_return = "";

                var ticket_data = File.ReadAllLines("C:\\CommandDesk_Tickets.txt");

                foreach (var ticket in ticket_data)
                {
                    if (ticket.Split('~')[0] == type.ToString() && ticket.Split('~')[1] == username)
                    {
                        count += 1;

                        ticket_return += (count.ToString() + "~" + ticket + "\n");
                    }
                    else if (ticket.Split('~')[3] == type.ToString() && ticket.Split('~')[4] == username)
                    {
                        count += 1;

                        ticket_return += (count.ToString() + "~" + ticket + "\n");
                    }
                }

                if (count > 0)
                    return ticket_return;
                else
                    return "1~NO TICKETS IN SYSTEM FOR USER";
            }
            else
                return "1~NO TICKETS IN SYSTEM";

        }

        private static void editLine(Ticket ticket)
        {
            if (File.Exists("C:\\CommandDesk_Tickets.txt"))
            {
                var index = 0;
                var ticket_data = File.ReadAllLines("C:\\CommandDesk_Tickets.txt");

                foreach(var row in ticket_data)
                {
                    var splits = row.Split('~');
                    if (splits[0] == ticket.RequesterClientType &&
                        splits[1] == ticket.RequesterUserName &&
                        splits[2] == ticket.IssueDescription &&
                        splits[3] == ticket.TechnicianClientType &&
                        splits[4] == ticket.TechnicianUserName)
                    {
                        ticket_data[index] = ticket.RequesterClientType +
                        "~" + ticket.RequesterUserName +
                        "~" + ticket.IssueDescription +
                        "~" + ticket.TechnicianClientType +
                        "~" + ticket.TechnicianUserName +
                        "~" + ticket.TechnicianResponse +
                        "|" + ticket.Status.ToString() + "|";

                    }

                    index += 1;
                }

                File.WriteAllLines("C:\\CommandDesk_Tickets.txt", ticket_data);
            }
        }
    }
}
