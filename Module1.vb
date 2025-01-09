Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Module Module1

    Private Const ConfigFileName As String = "tle_config.txt"

    Sub Main()
        Console.ForegroundColor = ConsoleColor.Cyan
        Console.WriteLine("THE APPLICATION REQUESTS TLE FOR GIVEN SATELLITES AND SAVES IT TO THE FILE" & vbNewLine)
        System.Threading.Thread.CurrentThread.CurrentUICulture = New Globalization.CultureInfo("en-US")
        Console.ForegroundColor = ConsoleColor.Gray
        Dim fn As String = GenerateTleFileName()

        Try
            CheckConfigFile()

            Dim apiAddr As String
            Dim apikey As String
            Dim satIds As New List(Of String)
            Dim tles As New List(Of String)

            Using fsr As New FileStream(ConfigFileName, FileMode.Open), sr As New StreamReader(fsr)

                apiAddr = sr.ReadLine().Trim()
                apikey = sr.ReadLine().Trim()

                Do While (sr.Peek <> -1)
                    Dim satId As String = sr.ReadLine().Trim()
                    satIds.Add(satId)
                Loop
            End Using

            Parallel.ForEach(satIds, New Action(Of String)(
                             Sub(satId As String)
                                 Dim response As String = GetResponseWithTleData(apiAddr, apikey, satId)
                                 Dim tle As String = ParseResponse(response)
                                 tles.Add(tle)
                             End Sub))

            Using fsw As New FileStream(fn, FileMode.OpenOrCreate), sw As New StreamWriter(fsw)
                For Each tle In tles
                    sw.WriteLine(tle)
                Next
            End Using

            Console.ForegroundColor = ConsoleColor.DarkCyan
            Console.WriteLine($"Result saves to [ {fn} ].")
            Console.ForegroundColor = ConsoleColor.Gray

        Catch ex As Exception
            Console.ForegroundColor = ConsoleColor.Red
            Console.WriteLine(ex.ToString())
        Finally
            Try
                Dim fi As New FileInfo(fn)
                If (fi.Length < 10) Then
                    fi.Delete()
                End If
            Catch ex As Exception
                Debug.WriteLine(ex.Message)
            End Try

            Console.ForegroundColor = ConsoleColor.Gray
            Console.WriteLine("Press any key...".Insert(0, vbNewLine))
            Console.ReadKey()
        End Try
    End Sub

    ''' <summary>
    ''' Разбирает ответ сервера и приводит его к формату TLE.
    ''' </summary>
    ''' <remarks>
    ''' Ответ выглядит так:
    ''' {"info":
    '''     {"satid":44453,"satname":"MERIDIAN 8","transactionscount":0},
    '''     "tle":"1 44453U 19046A   20082.44006892  .00000022  00000-0  00000+0 0  9998\r\n2 44453  62.7307 313.4060 7170897 271.6132 197.9595  2.00611004  4738"}
    ''' </remarks>
    ''' <param name="responseLine"></param>
    Private Function ParseResponse(responseLine As String) As String
        Try
            Dim satNameRegex As New Regex("(?i)""satname"":""(?<name>[a-z \-\d\(\)_]+)")
            Dim satnameMc As Match = satNameRegex.Match(responseLine)
            Dim satName As String = satnameMc.Groups("name").Value

            Dim tleRegex As New Regex("(?i)""tle"":""(?:(?<line1>[a-z \d\+\-\.]+)\\r\\n(?<line2>[a-z \d\+\-\.]+))""")
            Dim tleMc As Match = tleRegex.Match(responseLine)
            Dim line1 As String = tleMc.Groups("line1").Value
            Dim line2 As String = tleMc.Groups("line2").Value

            Dim sb As New StringBuilder()
            sb.AppendLine(satName)
            sb.AppendLine(line1)
            sb.AppendLine(line2)
            Return sb.ToString()

        Catch ex As Exception
            SyncLock ConsoleLock
                Console.WriteLine(ex.Message)
            End SyncLock
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' Генерирует имя файла по текущей дате.
    ''' </summary>
    Private Function GenerateTleFileName() As String
        Return $"{Now:yyyy-MM-dd HH-mm}.tle"
    End Function

    Private ReadOnly ConsoleLock As New Object()

    ''' <summary>
    ''' Получает для заданного по идентификатору КА ответ, содержащий TLE и другую информацию.
    ''' </summary>
    ''' <param name="apiAddr">Адрес для запроса.</param>
    ''' <param name="apiKey">Ключ для доступа к API.</param>
    ''' <param name="satelliteId">ID КА по классификации NORAD.</param>
    Private Function GetResponseWithTleData(apiAddr As String, apiKey As String, satelliteId As String) As String
        Dim resp As String = ""
        SyncLock ConsoleLock
            Console.ForegroundColor = ConsoleColor.Yellow
            Console.WriteLine($"Request TLE for {satelliteId}...")

            Dim apiUrl As String = $"{apiAddr}tle/{satelliteId}"
            If (apiKey.Length > 0) Then
                apiUrl &= $"&apiKey={apiKey}"
            End If
            Debug.WriteLine("API: " & apiUrl)

            ServicePointManager.Expect100Continue = True
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 Or SecurityProtocolType.Ssl3 Or SecurityProtocolType.Tls Or SecurityProtocolType.Tls11
            ServicePointManager.ServerCertificateValidationCallback = Function() True

            Dim req As HttpWebRequest = CType(HttpWebRequest.Create(apiUrl), HttpWebRequest)
            req.Method = WebRequestMethods.Http.Get
            req.ProtocolVersion = HttpVersion.Version11
            req.Accept = "application/json"
            req.AllowAutoRedirect = True
            req.KeepAlive = True
            req.Timeout = 5000

            'Получаем HTTP-ответ:
            Dim webResp As WebResponse = req.GetResponse()
            Using httpStm As New StreamReader(webResp.GetResponseStream())
                resp = httpStm.ReadToEnd().Trim()
            End Using

            Console.ForegroundColor = ConsoleColor.Gray
            Console.WriteLine(resp)
            Console.WriteLine()
        End SyncLock

        Return resp
    End Function

    ''' <summary>
    ''' Проверяет наличие конфиг. файла, при отсутствии создаёт. 
    ''' Добавляет 4 строчки: web-адрес, ключ API и ID 2-х спутников по классификации NORAD.
    ''' </summary>
    Private Sub CheckConfigFile()
        Dim fi As New FileInfo(ConfigFileName)
        If (Not fi.Exists) Then
            Using fs As New FileStream(fi.FullName, FileMode.CreateNew), sw As New StreamWriter(fs)
                sw.WriteLine("https://api.n2yo.com/rest/v1/satellite/") 'добавляет web-адрес для обращения к API 'IP=158.69.117.9
                sw.WriteLine("NQNYAH-JKSXC9-GRBAMJ-4BXK") 'добавляет ключ API
                'добавляет ID:
                sw.WriteLine("25544") 'МКС
                sw.WriteLine("33591") 'NORAD-19
                sw.WriteLine("29155") 'GOES-13
            End Using

            Console.WriteLine($"Configuration file [ {ConfigFileName} ] created." & vbNewLine)
        Else
            Console.WriteLine($"Configuration file [ {ConfigFileName} ] found." & vbNewLine)
        End If
    End Sub

End Module
