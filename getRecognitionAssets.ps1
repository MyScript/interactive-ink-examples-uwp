Set-PSDebug -Trace 1
$shell_app=new-object -com shell.application
$destination = $shell_app.namespace("$PSScriptRoot")


if (-not[System.IO.File]::Exists("$PSScriptRoot\recognition-assets\conf\diagram.conf") -Or
  -not[System.IO.File]::Exists("$PSScriptRoot\recognition-assets\conf\raw-content.conf")    -Or
  -not[System.IO.File]::Exists("$PSScriptRoot\recognition-assets\conf\math.conf")    -Or
  -not[System.IO.File]::Exists("$PSScriptRoot\recognition-assets\conf\en_US.conf"))

  {
    if ( Test-Path "$PSScriptRoot\recognition-assets")
    {
      Remove-Item "$PSScriptRoot\recognition-assets\*" -Recurse
    }

    # Download myscript-iink-recognition-math.zip
    $clnt = new-object System.Net.WebClient
    $url = "https://s3-us-west-2.amazonaws.com/iink/assets/2.0.1/myscript-iink-recognition-math.zip"
    $file = "$PSScriptRoot\myscript-iink-recognition-math.zip"

    $clnt.DownloadFile($url,$file)

    # Unzip myscript-iink-recognition-math.zip

    $zip_file = $shell_app.namespace($file)
    $destination.Copyhere($zip_file.items(),16)
    Remove-Item $file

    # Download myscript-iink-recognition-diagram.zip

    $clnt = new-object System.Net.WebClient
    $url = "https://s3-us-west-2.amazonaws.com/iink/assets/2.0.1/myscript-iink-recognition-diagram.zip"
    $file = "$PSScriptRoot\myscript-iink-recognition-diagram.zip"
    $clnt.DownloadFile($url,$file)

    # Unzip myscript-iink-recognition-diagram.zip

    $zip_file = $shell_app.namespace($file)
    $destination.Copyhere($zip_file.items(),16)
    Remove-Item $file

    # Download myscript-iink-recognition-raw-content.zip

    $clnt = new-object System.Net.WebClient
    $url = "https://s3-us-west-2.amazonaws.com/iink/assets/2.0.1/myscript-iink-recognition-raw-content.zip"
    $file = "$PSScriptRoot\myscript-iink-recognition-raw-content.zip"
    $clnt.DownloadFile($url,$file)

    # Unzip myscript-iink-recognition-raw-content.zip

    $zip_file = $shell_app.namespace($file)
    $destination.Copyhere($zip_file.items(),16)
    Remove-Item $file

    # Download myscript-iink-recognition-text-en_US.zip

    $clnt = new-object System.Net.WebClient
    $url = "https://s3-us-west-2.amazonaws.com/iink/assets/2.0.1/myscript-iink-recognition-text-en_US.zip"
    $file = "$PSScriptRoot\myscript-iink-recognition-text-en_US.zip"
    $clnt.DownloadFile($url,$file)

    # Unzip myscript-iink-recognition-text-en_US.zip

    $zip_file = $shell_app.namespace($file)
    $destination.Copyhere($zip_file.items(),16)
    Remove-Item $file
  }
