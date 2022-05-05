using System;
using System.Runtime;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace Command_Desk
{
    internal class Clientside
    {
        public static void StartClient()
        {
            var CLIENT_IP_ADDRESS = IPAddress.Parse(Program.SERVER_IP_STRING);
            var CLIENT_IP_ENDPOINT = new IPEndPoint(CLIENT_IP_ADDRESS, Program.PORT_INT);
            var CLIENT_SOCKET = new Socket(CLIENT_IP_ADDRESS.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var message = "";
            var username = "";
            var password = "";


            try
            {
                CLIENT_SOCKET.Connect(CLIENT_IP_ENDPOINT);
                Console.WriteLine("Connected to {0}", CLIENT_SOCKET.RemoteEndPoint);
            }
            catch (SocketException e) { Console.WriteLine(e); };


            if (CLIENT_IP_ADDRESS != null && CLIENT_IP_ENDPOINT != null)
            {
                if (CLIENT_SOCKET != null && CLIENT_SOCKET.Connected)
                {
                    // The server must initially send the client a 'hello' message upon connection
                    var stage = 0;
                    
                    while (stage >= 0)
                    {
                        // Limit active threads to MAX_CLIENTS to avoid out of memory exceptions.
                        // This is essentially the maximum number of active clients on the server, techs or users alike.
                        // I don't remember having this issue while doing the class assignments, but this is a good enough workaround for this product.
                        if (Program.ACTIVE < Program.MAX_CLIENTS)
                            try
                            {
                                Program.ACTIVE++;
                                var increase = false;

                                message = Program.Message(CLIENT_SOCKET, new byte[1024]);

                                Client server = new Client(message);

                                Program.COMMAND_APPEND_TARGET = "@" + server.sender;

                                var intendedTarget = (server.sender == ClientType.SERVER && server.target == Helpers.getTypeFromAppend(Program.COMMAND_APPEND_SENDER));


                                // Tells server what type of client they are
                                if (stage == 0 && message == Program.COMMAND_SERVER_TO_CLIENT_HELLO)
                                {
                                    increase = true;
                                    CLIENT_SOCKET.Send(Program.Command(String.Format(Program.COMMAND_CLIENT_TYPE, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));
                                }

                                // If the SERVER does not respond with the 'hello' message to the current client type, terminate the program
                                if (stage == 1 && intendedTarget && (message == Program.COMMAND_SERVER_TO_USER_HELLO || message == Program.COMMAND_SERVER_TO_TECH_HELLO))
                                {
                                    Console.WriteLine("Please enter your username (A-Z, a-z, 0-9): ");
                                    username = Console.ReadLine();

                                    while (string.IsNullOrWhiteSpace(username) || !username.All(char.IsLetterOrDigit))
                                    {
                                        Console.WriteLine("Bad username. Please enter a username using ONLY letters and/or numbers (A-Z, a-z, 0-9)");
                                        username = Console.ReadLine();
                                    }

                                    Console.WriteLine("Please enter your password: ");
                                    password = Console.ReadLine();

                                    while (string.IsNullOrWhiteSpace(password))
                                    {
                                        Console.WriteLine("Password Required! Please enter your password: ");
                                        password = Console.ReadLine();
                                    }

                                    switch (server.target)
                                    {
                                        case ClientType.USER:
                                        case ClientType.TECH:
                                            sendCredentials(CLIENT_SOCKET, username.ToUpper(), password.GetHashCode().ToString());
                                            increase = true;
                                            break;
                                        default:
                                            stage = -1;
                                            break;
                                    }

                                    Console.WriteLine(message);
                                }

                                if (stage == 2 && intendedTarget && message == string.Format(Program.COMMAND_VALID_LOGIN, server.target_append, server.sender_append))
                                {
                                    //Uncomment below for a fun time.
                                    //if (username.Equals("Brian", StringComparison.OrdinalIgnoreCase))
                                    //    Process.Start("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

                                    increase = true;
                                    CLIENT_SOCKET.Send(Program.Command(string.Format(Program.COMMAND_GOING_MENU, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));
                                }
                                else if (stage == 2 && intendedTarget && message == string.Format(Program.COMMAND_INVALID_LOGIN, server.target_append, server.sender_append))
                                {
                                    increase = false;


                                    // Reset stage to enter user and password again.
                                    CLIENT_SOCKET.Send(Program.Command(String.Format(Program.COMMAND_CLIENT_TYPE, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));
                                    stage = 1;
                                }
                                else if (stage == 2)
                                {
                                    Console.WriteLine(string.Format(Program.COMMAND_VALID_LOGIN, server.target_append, server.sender_append));
                                    
                                    stage = -1;
                                }

                                if (stage == 3 && intendedTarget && message == string.Format(Program.COMMAND_OK, server.target_append, server.sender_append))
                                {
                                    int pos = 0;
                                    bool onMenu = true;

                                    string[] selector = new string[3];
                                    revalSelectorPos(pos, selector);

                                    // YOU ARE HERE, READY TO GIVE AN AUTHENTICATED USER OPTIONS TO SELECT FROM.
                                    reprintMenu(username, selector[0], selector[1], selector[2]);

                                    ConsoleKey key;

                                    while (!Console.KeyAvailable && onMenu)
                                    {

                                        key = Console.ReadKey(false).Key;

                                        switch (key)
                                        {
                                            // set menu position
                                            case ConsoleKey.UpArrow:
                                                pos = (pos == 0 ? 2 : pos - 1);
                                                revalSelectorPos(pos, selector);
                                                reprintMenu(username, selector[0], selector[1], selector[2]);
                                                break;
                                            case ConsoleKey.DownArrow:
                                                pos = (pos == 2 ? 0 : pos + 1);
                                                revalSelectorPos(pos, selector);
                                                reprintMenu(username, selector[0], selector[1], selector[2]);
                                                break;



                                            case ConsoleKey.N:
                                                // GO TO NEW TICKET
                                                revalSelectorPos(pos, selector);
                                                reprintMenu(username, selector[0], selector[1], selector[2]);

                                                stage = 4;
                                                CLIENT_SOCKET.Send(Program.Command(createNewTicket(username, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));

                                                onMenu = false;
                                                break;



                                            case ConsoleKey.V:
                                                // GO TO VIEW TICKETS
                                                revalSelectorPos(pos, selector);
                                                reprintMenu(username, selector[0], selector[1], selector[2]);

                                                stage = 5;
                                                CLIENT_SOCKET.Send(Program.Command(string.Format(Program.COMMAND_GOING_VIEW_TICKET, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));

                                                onMenu = false;
                                                break;



                                            case ConsoleKey.C:
                                                // GO TO CLOSE TICKETS
                                                revalSelectorPos(pos, selector);
                                                reprintMenu(username, selector[0], selector[1], selector[2]);

                                                stage = 6;
                                                CLIENT_SOCKET.Send(Program.Command(string.Format(Program.COMMAND_GOING_CLOSE_TICKET, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));

                                                onMenu = false;
                                                break;



                                            case ConsoleKey.Enter:
                                                // GO TO SELECTED
                                                revalSelectorPos(pos, selector);
                                                reprintMenu(username, selector[0], selector[1], selector[2]);

                                                increase = true;
                                                string select = (pos == 0 ? createNewTicket(username, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER) : (pos == 1 ? Program.COMMAND_GOING_VIEW_TICKET : Program.COMMAND_GOING_CLOSE_TICKET));

                                                CLIENT_SOCKET.Send(Program.Command(string.Format(select, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));
                                                onMenu = false;
                                                break;
                                            default:
                                                break;

                                        }
                                    }
                                }
                                else if (stage == 3)
                                {
                                    stage = -1;
                                }

                                if (stage == 4 && intendedTarget && message == string.Format(Program.COMMAND_OK, server.target_append, server.sender_append))
                                {
                                    // Go back up to menu.
                                    stage = 3;
                                    CLIENT_SOCKET.Send(Program.Command(string.Format(Program.COMMAND_GOING_MENU, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));
                                }
                                if ((stage == 5 || stage == 6) && intendedTarget && message.StartsWith(string.Format(Program.COMMAND_TICKET_LIST,"","","")))
                                {
                                    int pos = 0;
                                    bool onTicketList = true;
                                    int max = message.Split('\n').Length - 1;

                                    reprintTickets(pos, message);

                                    ConsoleKey key;

                                    while (!Console.KeyAvailable && onTicketList)
                                    {

                                        key = Console.ReadKey(false).Key;

                                        switch (key)
                                        {
                                            // set menu position
                                            case ConsoleKey.UpArrow:
                                                pos = (pos == 0 ? max : pos - 1);
                                                reprintTickets(pos, message);
                                                break;
                                            case ConsoleKey.DownArrow:
                                                pos = (pos == max ? 0 : pos + 1);
                                                reprintTickets(pos, message);
                                                break;

                                            case ConsoleKey.R:
                                                if (Helpers.getTypeFromAppend(Program.COMMAND_APPEND_SENDER) == ClientType.TECH)
                                                {
                                                    var short_message = message.Substring(13);
                                                    var ticket = new Ticket(short_message.Split('\n')[pos]);
                                                    var response = addResponseToTicket(ticket);

                                                    var compiled = ticket.RequesterClientType + "~" +
                                                                   ticket.RequesterUserName + "~" +
                                                                   ticket.IssueDescription + "~" +
                                                                   ticket.TechnicianClientType + "~" +
                                                                   ticket.TechnicianUserName + "~" +
                                                                   response + "|" +
                                                                   ticket.Status.ToString() + "|";

                                                    CLIENT_SOCKET.Send(Program.Command(string.Format(Program.COMMAND_EDIT_TICKET, compiled, Program.COMMAND_APPEND_SENDER, Program.COMMAND_APPEND_TARGET)));

                                                    reprintTickets(pos, message);
                                                }
                                                break;
                                            case ConsoleKey.Enter:
                                                increase = true;

                                                if (pos == max)
                                                {
                                                    stage = 3;
                                                    CLIENT_SOCKET.Send(Program.Command(string.Format(Program.COMMAND_GOING_MENU, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));
                                                    onTicketList = false;
                                                }
                                                else
                                                {
                                                    // This is where I would implement other features onto a ticket after selection.
                                                    // Since the MVP did not outline an action for a user onto a ticket besides delete,
                                                    // hitting enter on the ticket will go back to menu as well.

                                                    var short_message = message.Substring(13);
                                                    var ticket = new Ticket(short_message.Split('\n')[pos]);
                                                    var compiled = ticket.RequesterClientType + "~" +
                                                                   ticket.RequesterUserName + "~" +
                                                                   ticket.IssueDescription + "~" +
                                                                   ticket.TechnicianClientType + "~" +
                                                                   ticket.TechnicianUserName + "~" +
                                                                   ticket.TechnicianResponse + "|" +
                                                                   (ticket.Status == TicketStatus.OPEN ? TicketStatus.CLOSED.ToString() : TicketStatus.OPEN.ToString()) + "|";

                                                    CLIENT_SOCKET.Send(Program.Command(string.Format(Program.COMMAND_EDIT_TICKET, compiled, Program.COMMAND_APPEND_SENDER, Program.COMMAND_APPEND_TARGET)));
                                                }
                                                break;
                                            default:
                                                break;

                                        }
                                    }
                                }

                                if (increase)
                                stage++;

                                Program.ACTIVE--;
                            }
                            catch (SocketException e)
                            {
                                Console.WriteLine(e);
                            }
                    }

                    Console.WriteLine("Connection closed to server {0}", CLIENT_SOCKET.RemoteEndPoint);
                    CLIENT_SOCKET.Shutdown(SocketShutdown.Both);
                    CLIENT_SOCKET.Close();
                }
            }
        }

        private static void sendCredentials(Socket handler, String username, String password)
        {
            handler.Send(Program.Command(string.Format("{0}~{1}{2}{3}", username, password, Program.COMMAND_APPEND_TARGET, Program.COMMAND_APPEND_SENDER)));
        }

        private static void reprintMenu(String username, String opt1, String opt2, String opt3)
        {
            Console.Clear();
            Console.WriteLine("Welcome {0}!\n\n", username);

            Console.WriteLine("Please select from the menu below (use arrow keys or hotkey): ");
            Console.WriteLine("{0} [N]ew Ticket", opt1);
            Console.WriteLine("{0} [V]iew Active Tickets", opt2);
            Console.WriteLine("{0} [C]lose Ticket from List", opt3);
        }

        private static void revalSelectorPos(int pos, string[] selector)
        {
            switch (pos)
            {
                case 0:
                    selector[0] = "-->";
                    selector[1] = "   ";
                    selector[2] = "   ";
                    break;
                case 1:
                    selector[0] = "   ";
                    selector[1] = "-->";
                    selector[2] = "   ";
                    break;
                case 2:
                    selector[0] = "   ";
                    selector[1] = "   ";
                    selector[2] = "-->";
                    break;
                default:
                    selector[0] = "   ";
                    selector[1] = "   ";
                    selector[2] = "   ";
                    break;
            }
        }

        private static string createNewTicket(string username, String target, String sender)
        {
            Console.WriteLine("Welcome {0}!\n\n", username);

            string accepted = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,`!#$%^&*()-=_+'\"\\/<>{};:";

            bool issue = true;

            string description = "";

            while (issue)
            {
                Console.WriteLine("Allowed Characters ( no ~ @ [] | ): [ a-z ], [ A-Z ], [ 0-9 ], [ .,`!#$%^&*()-=_+'\"\\/<>{};: ] \n");
                Console.WriteLine("Please enter a description of your issue: ");

                description = Console.ReadLine();
                issue = false;

                // check if description is valid characters.
                if (String.IsNullOrEmpty(description))
                    issue = true;

                foreach (var c in description)
                    if (!accepted.Contains(c))
                        issue = true;
            }

            return Program.COMMAND_GOING_NEW_TICKET + "~" + Helpers.getTypeFromAppend(sender) + "~" + username + "~" + description + "~~~~"+ target + sender;
        }

        private static void reprintTickets(int pos, string message)
        {
            var msg = message.Substring(13);

            Ticket[] tickets = new Ticket[msg.Split('\n').Length - 1];

            for (int i = 0; i < msg.Split('\n').Length - 1; i++)
                tickets[i] = new Ticket(msg.Split('\n')[i]);

            Console.Clear();
            Console.WriteLine("VIEWING TICKETS FOR " + Helpers.getTypeFromAppend(Program.COMMAND_APPEND_SENDER) + ": " + tickets[0].RequesterUserName + "\n\n");
            Console.WriteLine("[UP] and [DOWN] to cycle through menu.\n[Enter] toggles status on ticket or returns to menu.\n" + (Helpers.getTypeFromAppend(Program.COMMAND_APPEND_SENDER) == ClientType.TECH ? "[R] lets you respond to a ticket." : "") + "\n\n");

            for (int j = 0; j < tickets.Length; j++)
            {
                Console.WriteLine((pos == j ? "-->\t" : "\t") + tickets[j].Order + " - " + tickets[j].Status + " - " + tickets[j].IssueDescription + "\n");
                Console.WriteLine("\t\t" + tickets[j].RequesterClientType + ": " + tickets[j].RequesterUserName + "\n");
                Console.WriteLine("\t\t" + tickets[j].TechnicianClientType + ": " + tickets[j].TechnicianUserName + "\n");

                var temp_response = tickets[j].TechnicianResponse;
                for (int k = 0; k < temp_response.Length; k++)
                    temp_response.Replace('^', '\n');
                Console.WriteLine((pos == j ? "\t" : "\t") + "\t" + "Technician Response: " + temp_response + "\n");
            }

            Console.WriteLine((pos == tickets.Length ? "-->\t" : "\t") + "\n" + (tickets.Length + 1) + " - Back to Main Menu!");
        }

        private static string addResponseToTicket(Ticket ticket)
        {
            Console.Clear();
            Console.WriteLine("TECH, Please enter a response for this ticket.\n");
            Console.Write("-->: ");
            var response = Console.ReadLine();

            ticket.TechnicianResponse = response + "^" + ticket.TechnicianResponse;

            return response;
        }
    }
}
