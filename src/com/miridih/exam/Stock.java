package com.miridih.exam;

import java.util.*;
import java.io.*;
import java.lang.reflect.Array;

/**
 * 주식 투자 프로그램의 스켈레톤
 * */
public class Stock implements Solver {

	public static void main(String[] args) {
		try {
			new Stock().solve(new FileInputStream("C:/Users/Administrator/Downloads/TestFile/주식투자-예시문제.txt"), System.out);
		} catch (FileNotFoundException e) {
			// TODO Auto-generated catch block
			e.printStackTrace();
		}        
	}

	/**
	 * 문제 풀이 메인 메소드   
	 * @param in 테스트 케이스 입력을 받이들이는 InputStream
	 * @param out 결과값을 출력할 PrintStream 
	 */

	@Override
	public void solve(InputStream in, PrintStream out) {
		// TODO Auto-generated method stub
		try {
			Reader reader = new InputStreamReader(in);
			BufferedReader br = new BufferedReader(reader);
			String firstLine = br.readLine();
			int testCase = Integer.parseInt(firstLine);
			out.println("testCase : " + testCase);
			int first = 1;
//			while(testCase!=0) {
				System.out.println(first+"번째 계산");
				first++;
				int dayN = Integer.parseInt(br.readLine());
				out.println("dayN : " + dayN);
				String[] spaceChecker = br.readLine().split(" ");
				if(dayN != spaceChecker.length) {
					out.println("날짜 개수와 주식 개수가 일치하지 않습니다.");
				}
				int[] stockMoney = new int[dayN];
				int count = 1;
				for(int i = 0; i < dayN; i++) {
					stockMoney[i] = Integer.parseInt(spaceChecker[i]);
					count *= 3;
				}
				//총 이윤
				long benefit = 0;
				int myStock = 0;
				
				String[] result = new String[count];
				Arrays.fill(result,"");
				System.out.println(result.length);
				for(int j = 0; j < result.length ; j++) {
					
				}
				
				/*//최대 이윤을 체크하기 위한 배열
				long[] maxBenefitCheck = new long[dayN];
				Arrays.fill(maxBenefitCheck, 0);
				long maxValue = 0;
				
				for(int j = 0; j < dayN; j++) {
					//나의 총 주식 개수
					int myStock = 0;
					//주식을 총 얼마주고 샀는지를 위한 변수
					int buyStock = 0;
					for(int k = j; k < dayN-1; k++) {
						myStock++;
						buyStock += stockMoney[k];
						maxBenefitCheck[k+1] += stockMoney[k+1]*myStock - buyStock;
					}
					
				}
				for(long l : maxBenefitCheck) {
					System.out.println(l);
					if(maxValue < l) {
						maxValue = l;
					}
				}
				System.out.println(maxValue);
				*/
//				while(saveDays<dayN) {
//					//최대값이 정해져있을 때의 나의 주식 개수
//					int maxMyStock = 0;
//					//나의 총 주식 개수
//					int myStock = 0;
//					//주식을 총 얼마주고 샀는지를 위한 변수
//					int buyStock = 0;
//					//모든 이윤 초기화
//					Arrays.fill(maxBenefitCheck, 0);
//					//배열속에서 가장 많은 이윤을 가진 값
//					long maxValue = 0;
//					for(int j = saveDays+1; j<dayN; j++) {
//						myStock++;
//						buyStock += stockMoney[j-1];
//						maxBenefitCheck[j] = stockMoney[j]*myStock - buyStock;
////						System.out.println(maxBenefitCheck[j]);
//						if(maxValue < maxBenefitCheck[j]) {
//							maxMyStock = myStock;
//							maxValue = maxBenefitCheck[j];
//						}
//
//					}
////					System.out.println(maxValue);
//					for(int k = 0 ; k < maxBenefitCheck.length; k++) {
//						if(maxValue == 0) {
//							saveDays++;
////							System.out.println("주식을 사지 않음");
//							break;
//						}
//						if(maxValue == maxBenefitCheck[k]) {
//							saveDays = k+1;
//							
//							benefit += maxValue;
//						}
//					}
////					System.out.println(saveDays+" 날까지 구매 최대값과 최대값의 스톡\n" + maxValue + " 주식 몇개를 샀는가? " + maxMyStock);
//				}
				
				System.out.println("benefit : " + benefit);
				testCase--;
//			}
			out.println("test 종료");
		} catch (IOException e) {
			// TODO Auto-generated catch block
			e.printStackTrace();
		} finally {
			try {
				if(in != null) {
					in.close();
				}
				if(out != null) {
					out.close();
				}
			} catch (IOException e) {
				// TODO Auto-generated catch block
				e.printStackTrace();
			}
		}
		

	}

}
