// Five consecutive text chunks for the Cloud Console editor. Joining is lossless.
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
export function splitIndexFive(source) {
  const lines=source.match(/[^\n]*\n|[^\n]+$/g)??[];
  if(lines.length<5)throw new Error('At least five source lines are required.');
  let at=0,remaining=Buffer.byteLength(source);const parts=[];
  for(let part=0;part<4;part++){
    const wanted=remaining/(5-part),start=at;let length=0;
    while(at<lines.length-(4-part)){
      const next=Buffer.byteLength(lines[at]);
      if(at>start&&Math.abs(length-wanted)<Math.abs(length+next-wanted))break;
      length+=next;at++;
    }
    parts.push(lines.slice(start,at).join(''));remaining-=length;
  }
  parts.push(lines.slice(at).join(''));
  if(parts.some(part=>!part)||parts.join('')!==source)throw new Error('Source split verification failed.');
  return parts;
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const [input,directory]=process.argv.slice(2);
  if(!input||!directory)throw new Error('Usage: node split-index-five.mjs updated-index.js output-directory');
  const source=readFileSync(input,'utf8'),parts=splitIndexFive(source);mkdirSync(directory,{recursive:true});
  parts.forEach((part,i)=>writeFileSync(join(directory,`index-teil-${i+1}-von-5.txt`),part,{flag:'wx'}));
  console.log('Five consecutive parts written and verified. SHA-256: '+createHash('sha256').update(source).digest('hex'));
}
