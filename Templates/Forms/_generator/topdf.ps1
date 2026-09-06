param([string]$Dir)
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
Get-ChildItem -Path $Dir -Filter *.docx | ForEach-Object {
    $src = $_.FullName
    $dst = [System.IO.Path]::ChangeExtension($src, ".pdf")
    $doc = $word.Documents.Open($src, $false, $true)
    $doc.SaveAs([ref]$dst, [ref]17)
    $doc.Close([ref]0)
    Write-Output ("OK " + $_.Name + " pages=" + "")
}
$word.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
