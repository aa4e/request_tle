Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

#Const RU = False

Module Module1

    Private Const ConfigFileName As String = "tle_config.txt"

    Sub Main()
#If DEBUG Then
        ''Dim res As String = "{""info"":{""satid"":44453,""satname"":""MERIDIAN 8"",""transactionscount"":0},""tle"":""1 44453U 19046A   20082.44006892  .00000022  00000-0  00000+0 0  9998\r\n2 44453  62.7307 313.4060 7170897 271.6132 197.9595  2.00611004  4738""}"
        'Dim res As String = "{""info"":{""satid"":47719,""satname"":""ARKTIKA-M 1"",""transactionscount"":2},""tle"":""1 47719U 21016A   23247.62710293  .00000127  00000-0  00000-0 0  9996\r\n2 47719  63.1483 183.8473 6888953 269.5248  18.0620  2.00592599 18391""}"
        'Dim r As String = ParseResponse(res)
        'Debug.WriteLine(r)
        'Return
#End If

        Console.ForegroundColor = ConsoleColor.Cyan
#If RU Then
        Console.WriteLine("ПРОГРАММА ЗАПРАШИВАЕТ TLE ДЛЯ ЗАДАННЫХ КА И СОХРАНЯЕТ В ФАЙЛЕ" & vbNewLine) 
        System.Threading.Thread.CurrentThread.CurrentUICulture = New Globalization.CultureInfo("ru-RU")
#Else
        Console.WriteLine("THE APPLICATION REQUESTS TLE FOR GIVEN SATELLITES AND SAVES IT TO THE FILE" & vbNewLine)
        System.Threading.Thread.CurrentThread.CurrentUICulture = New Globalization.CultureInfo("en-US")
#End If
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

#If RU Then
            Console.WriteLine($"Обнаружено {satIds.Count} ID КА." & vbNewLine)
#Else
            Console.WriteLine($"Found {satIds.Count} satellite IDs." & vbNewLine)
#End If

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
#If RU Then
            Console.WriteLine($"Результаты сохранены в файле [ {fn} ].")
#Else
            Console.WriteLine($"Result saves to [ {fn} ].")
#End If
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
#If RU Then
            Console.WriteLine("Нажмите любую клавишу...")
#Else
            Console.WriteLine("Press any key...".Insert(0, vbNewLine))
#End If
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
        Return $"{Now:yyyy-MM-dd HH-mm-ss}.tle"
    End Function

    Private ReadOnly ConsoleLock As New Object()

    ''' <summary>
    ''' Получает для заданного по идентификатору КА ответ, содержащий TLE и другую информацию.
    ''' </summary>
    ''' <param name="apiAddr">Адрес для запроса.</param>
    ''' <param name="apiKey">Ключ для доступа к API.</param>
    ''' <param name="satelliteId">ID КА по классификации NORAD.</param>
    Private Function GetResponseWithTleData(apiAddr As String, apiKey As String, satelliteId As String) As String

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

        Dim resp As String = ""
        Using httpStm As New StreamReader(webResp.GetResponseStream())
            resp = httpStm.ReadToEnd().Trim()
        End Using

        SyncLock ConsoleLock
            Console.ForegroundColor = ConsoleColor.Yellow
#If RU Then
            Console.WriteLine($"Запрос TLE для {satelliteId}...")
#Else
            Console.WriteLine($"Request TLE for {satelliteId}...")
#End If
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
                sw.WriteLine("11251") 'Метеор
                sw.WriteLine("47719") 'Арктика-М 1
                sw.WriteLine("33591") 'NORAD-19
                sw.WriteLine("29155") 'GOES-13
            End Using

#If RU Then
            Console.WriteLine($"Создан конфигурационный файл [ {ConfigFileName} ]." & vbNewLine)
#Else
            Console.WriteLine($"Configuration file [ {ConfigFileName} ] created." & vbNewLine)
#End If
        Else
#If RU Then
            Console.WriteLine($"Обнаружен конфигурационный файл [ {ConfigFileName} ]." & vbNewLine)
#Else
            Console.WriteLine($"Configuration file [ {ConfigFileName} ] found." & vbNewLine)
#End If
        End If
    End Sub

End Module
