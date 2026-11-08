const fs=require('fs');
const core=fs.readFileSync('core.js','utf8');
const src=fs.readFileSync('index.src.html','utf8');
if(core.includes('</script'))throw new Error('core contiene </script');
const page=src.replace('/*CORE*/',()=>core);
fs.writeFileSync('PetaloPop.html',page);
fs.writeFileSync('test.html','<!doctype html><meta charset=utf-8><meta name=viewport content="width=device-width,initial-scale=1,viewport-fit=cover"><style>:root{color-scheme:light}body{margin:0;font:14px system-ui}</style>'+page);
console.log('ok',page.length,'bytes');
