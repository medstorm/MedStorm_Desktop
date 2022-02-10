$A = $( 
    $count = 0
    $hostname = "127.0.0.1 medstorm"
    try{
        $file = "$env:windir\System32\drivers\etc\hosts"
        foreach ($line in Get-Content $file) 
        {
            if($line -eq $hostname)
            {
                Write-Host "line exist"
                $count = ($count + 1)
            } 
            else 
            {  
                Write-Host "line does not exist" 
            }
        }
        if($count -eq 0)
        {
            Write-Host "Line added to " $file
            $hostname | Add-Content -PassThru $file
        }
    }
    catch 
    {
        Write-Host "Error occured"
        Write-Host $_.ScriptStackTrace
    }
)