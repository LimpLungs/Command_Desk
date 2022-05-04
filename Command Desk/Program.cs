using System;
using System.Runtime;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using System.IO;

namespace Command_Desk
{
    internal class Program
    {
        // Should make getters and setters to all strings and make private.

        public static String SERVER_IP_STRING = "127.0.0.1";//Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();

        public const String MASTER_USER = "ADMIN"; // These would be configurable in an installer on release
        public const String MASTER_PASS = "ADMIN"; // These would be configurable in an installer on release
        public const int PORT_INT = 7777;
        public static int ACTIVE = 0;
        public const int MAX_CLIENTS = 255; // Tested up to 2048 and works, 4096 has out of memory exception on my configuration (Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz 3.60 GHz, 32 GB Ram @3600 Mhz).  Will limit at 255 to ensure wider compatibility.

        public static String COMMAND_APPEND_TARGET = "@TARGET"; // should be @target/sender
        public static String COMMAND_APPEND_SENDER = "[SENDER]"; // should be @target/sender

        public const String COMMAND_VALID_LOGIN = "LOGIN~TRUE{0}{1}";
        public const String COMMAND_INVALID_LOGIN = "LOGIN~FALSE{0}{1}";
        public const String COMMAND_CLIENT_TYPE = "RETURNCLIENTTYPE{0}{1}";

        public const String COMMAND_OK = "OK{0}{1}";
        public const String COMMAND_GOING_MENU = "GOINGMENU{0}{1}";
        public const String COMMAND_GOING_NEW_TICKET = "@NEW@";
        public const String COMMAND_GOING_VIEW_TICKET = "GOINGVIEWTICKET{0}{1}";
        public const String COMMAND_GOING_CLOSE_TICKET = "GOINGCLOSETICKET{0}{1}";

        public const String COMMAND_SERVER_TO_USER_HELLO = "hello@USER[S]";
        public const String COMMAND_SERVER_TO_TECH_HELLO = "hello@TECH[S]";
        public const String COMMAND_SERVER_TO_CLIENT_HELLO = "hello@CLIENT[S]";

        static void Main(string[] args)
        {
            var loop = true;

            while (loop)
            {
                var arg = "";

                loop = false;

                Console.WriteLine("Please select to [S]tart or [Q]uit the application.");
                var input = Console.ReadLine();

                if (String.Equals(input, "S", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length <= 0)
                    {
                        var subloop = true;

                        while (subloop)
                        {
                            subloop = false;

                            Console.WriteLine("No argument passed. Please select [S]erver, [T]echnician, [U]ser, or [Q]uit");
                            arg = Console.ReadLine();

                            if (!String.Equals(arg, "T", StringComparison.OrdinalIgnoreCase) && !String.Equals(arg, "U", StringComparison.OrdinalIgnoreCase) && !String.Equals(arg, "S", StringComparison.OrdinalIgnoreCase) && !String.Equals(arg, "Q", StringComparison.OrdinalIgnoreCase))
                                subloop = true;
                        }
                    }
                    else
                        arg = args[0];

                    COMMAND_APPEND_SENDER = string.Format("[{0}]", arg.ToUpper());

                    if (String.Equals(arg, "S", StringComparison.OrdinalIgnoreCase))
                        Serverside.StartServer();
                    else if (String.Equals(arg, "T", StringComparison.OrdinalIgnoreCase) || String.Equals(arg, "U", StringComparison.OrdinalIgnoreCase))
                        Clientside.StartClient();
                }
                else if (!String.Equals(input, "Q", StringComparison.OrdinalIgnoreCase))
                    loop = true;
            }


        }

        public static byte[] Command(String s)
        {
            return Encoding.ASCII.GetBytes(s);
        }

        public static String Message(Socket socket, byte[] buffer)
        {
            return Encoding.ASCII.GetString(buffer, 0, socket.Receive(buffer));
        }
    }
    public enum ClientType { USER, TECH, SERVER, NONE };

    class Packet
    {
        public Byte[] bytes = new Byte[1024];
        public int size = 0;
        public String message = "";

        public Packet(Socket handler)
        {
            size = handler.Receive(bytes);

            if (size > 0)
                message = Encoding.ASCII.GetString(bytes, 0, size);
        }
    }

    class Client
    {
        public ClientType target;
        public ClientType sender;
        public String target_append;
        public String sender_append;
        public Client(String data)
        {
            bool hasFlags = data.Length > 0 && 
                            data.LastIndexOf('@') > 0 && 
                            data.LastIndexOf('[') > 0 && 
                            data.LastIndexOf(']') > 0 ? true : false;

            String t = hasFlags ? data.Substring(data.LastIndexOf('@'), data.LastIndexOf('[') - data.LastIndexOf('@')) : "";
            String s = hasFlags ? data.Substring(data.LastIndexOf('['), data.LastIndexOf(']') - data.LastIndexOf('[') + 1) : "";

            target = Helpers.getTypeFromAppend(t);
            sender = Helpers.getTypeFromAppend(s);
            target_append = "@" + target.ToString();
            sender_append = "[" + sender.ToString().ElementAt(0) + "]";
        }
        public Client(ClientType target, ClientType sender)
        {
            this.target = target;
            this.sender = sender;
            target_append = "@" + target.ToString();
            sender_append = "[" + sender.ToString().ElementAt(0) + "]";
        }


    }

    class Helpers
    {
        public static ClientType getTypeFromAppend(String s)
        {
            switch (s)
            {
                case null:
                    return ClientType.USER;
                case "[U]":
                    return ClientType.USER;
                case "@USER":
                    return ClientType.USER;
                case "[T]":
                    return ClientType.TECH;
                case "@TECH":
                    return ClientType.TECH;
                case "[S]":
                    return ClientType.SERVER;
                case "@SERVER":
                    return ClientType.SERVER;
                default:
                    return ClientType.NONE;
            }
        }

    }
}
